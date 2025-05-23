using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Messenger
{
    public class TypingStatusEventArgs : EventArgs
    {
        public string SenderName { get; }
        public bool IsTyping { get; }
        public TypingStatusEventArgs(string senderName, bool isTyping)
        {
            SenderName = senderName;
            IsTyping = isTyping;
        }
    }

    public static class NetworkExtensions
    {
        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
            {
                var completedTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
                if (completedTask == tcs.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return await task.ConfigureAwait(false);
            }
        }

        public static async Task<TcpClient> AcceptTcpClientAsync(this TcpListener listener, CancellationToken token)
        {
            if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
            try
            {
                var acceptTask = listener.AcceptTcpClientAsync();
                var tcs = new TaskCompletionSource<bool>();
                using (token.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                {
                    if (acceptTask == await Task.WhenAny(acceptTask, tcs.Task).ConfigureAwait(false))
                    {
                        return await acceptTask.ConfigureAwait(false);
                    }
                    else
                    {
                        token.ThrowIfCancellationRequested();
                        return null;
                    }
                }
            }
            catch (ObjectDisposedException) when (token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();
                return null;
            }
        }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public ChatMessage Message { get; }
        public MessageReceivedEventArgs(ChatMessage message)
        {
            Message = message;
        }
    }

    public class PeerDiscoveryEventArgs : EventArgs
    {
        public string PeerName { get; }
        public IPEndPoint PeerEndPoint { get; }
        public PeerDiscoveryEventArgs(string peerName, IPEndPoint peerEndPoint)
        {
            PeerName = peerName;
            PeerEndPoint = peerEndPoint;
        }
    }

    public class NetworkService : IDisposable
    {
        private readonly UdpClient _udpClient;
        private TcpListener _tcpListener;
        private string _localUserName;
        private int _tcpPort;
        private readonly IPAddress _multicastAddress;
        private readonly int _multicastPort;

        private CancellationTokenSource _cancellationTokenSource;
        private Task _udpListenTask;
        private Task _tcpListenTask;
        private Task _heartbeatTask;
        private Task _peerCleanupTask;

        private ConcurrentDictionary<string, PeerInfo> _activePeers;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<PeerDiscoveryEventArgs> PeerDiscovered;
        public event EventHandler<PeerDiscoveryEventArgs> PeerDisconnected;
        public event EventHandler<TypingStatusEventArgs> TypingStatusReceived;

        public int ActualTcpPort => _tcpPort;

        public NetworkService(string userName, int tcpPort, string multicastAddress, int multicastPort)
        {
            _localUserName = userName;
            _multicastAddress = IPAddress.Parse(multicastAddress);
            _multicastPort = multicastPort;

            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _multicastPort));
            _udpClient.JoinMulticastGroup(_multicastAddress);

            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, tcpPort);
                _tcpListener.Start();
                _tcpPort = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _tcpListener = new TcpListener(IPAddress.Any, 0);
                _tcpListener.Start();
                _tcpPort = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
            }
            catch (Exception)
            {
                throw;
            }
            _activePeers = new ConcurrentDictionary<string, PeerInfo>();
        }

        public void UpdateLocalUserName(string newUserName)
        {
            if (string.IsNullOrWhiteSpace(newUserName))
            {
                throw new ArgumentException("Tên người dùng mới không được để trống.");
            }

            if (_activePeers.ContainsKey(newUserName) && newUserName != _localUserName)
            {
                throw new InvalidOperationException($"Tên người dùng '{newUserName}' đã được sử dụng bởi một peer khác.");
            }

            string oldUserName = _localUserName;
            _localUserName = newUserName;

            PeerInfo localPeerEntry;
            if (_activePeers.TryRemove(oldUserName, out localPeerEntry))
            {
                if (localPeerEntry.IsLocal)
                {
                    var newLocalPeerEntry = new PeerInfo(newUserName, localPeerEntry.EndPoint, true)
                    {
                        LastHeartbeat = localPeerEntry.LastHeartbeat
                    };
                    _activePeers.TryAdd(newUserName, newLocalPeerEntry);
                    Debug.WriteLine($"[NetworkService] Tên người dùng cục bộ đã được cập nhật trong _activePeers: {oldUserName} -> {newUserName}");
                }
                else
                {
                    _activePeers.TryAdd(oldUserName, localPeerEntry);
                    Debug.WriteLine($"[NetworkService] Cảnh báo: Đã cố gắng cập nhật người dùng cục bộ, nhưng mục nhập cho '{oldUserName}' không được đánh dấu là cục bộ. Đã thêm lại.");
                }
            }
            else
            {
                Debug.WriteLine($"[NetworkService] Thông tin: Mục nhập người dùng cục bộ cho '{oldUserName}' không tìm thấy trong _activePeers trong quá trình đổi tên thành '{newUserName}'. Đang thêm mục nhập mới.");
                if (!_activePeers.ContainsKey(newUserName) && _tcpListener != null && _tcpListener.Server.IsBound)
                {
                    var newEp = _tcpListener.LocalEndpoint as IPEndPoint ?? new IPEndPoint(IPAddress.Loopback, _tcpPort);
                    _activePeers.TryAdd(newUserName, new PeerInfo(newUserName, newEp, true));
                }
            }

            // Gửi thông báo cập nhật tên ngay lập tức
            Task.Run(() => SendNameUpdate(oldUserName, newUserName)).GetAwaiter().GetResult();
        }

        public async Task SendNameUpdate(string oldName, string newName)
        {
            try
            {
                string message = $"NAME_UPDATE:{oldName}:{newName}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _udpClient.SendAsync(data, data.Length, new IPEndPoint(_multicastAddress, _multicastPort));
                Debug.WriteLine($"[NetworkService] Đã gửi NAME_UPDATE: {oldName} -> {newName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkService] Lỗi gửi NAME_UPDATE: {ex.Message}");
                throw;
            }
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            if (_tcpListener == null || !_tcpListener.Server.IsBound)
            {
                try
                {
                    if (_tcpListener == null) _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
                    _tcpListener.Start();
                    _tcpPort = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Không thể khởi động TCP listener trong Start(): {ex.Message}");
                    throw new InvalidOperationException("NetworkService không thể khởi động TCP listener.", ex);
                }
            }

            _udpListenTask = Task.Run(() => ListenForUdpMessages(cancellationToken), cancellationToken);
            _tcpListenTask = Task.Run(() => ListenForTcpConnections(cancellationToken), cancellationToken);
            _heartbeatTask = Task.Run(() => SendHeartbeatPeriodically(cancellationToken), cancellationToken);
            _peerCleanupTask = Task.Run(() => CleanupDisconnectedPeers(cancellationToken), cancellationToken);

            var localEp = _tcpListener.LocalEndpoint as IPEndPoint;
            if (localEp != null)
            {
                _activePeers.AddOrUpdate(_localUserName,
                   new PeerInfo(_localUserName, localEp, true),
                   (key, existingInfo) => {
                       existingInfo.EndPoint = localEp;
                       existingInfo.LastHeartbeat = DateTime.UtcNow;
                       return existingInfo;
                   });
            }
            else
            {
                Debug.WriteLine("[NetworkService] Cảnh báo: Không thể lấy IPEndPoint cục bộ để thêm vào active peers.");
                _activePeers.AddOrUpdate(_localUserName,
                   new PeerInfo(_localUserName, new IPEndPoint(IPAddress.Loopback, _tcpPort), true),
                   (key, existingInfo) => {
                       existingInfo.EndPoint = new IPEndPoint(IPAddress.Loopback, _tcpPort);
                       existingInfo.LastHeartbeat = DateTime.UtcNow;
                       return existingInfo;
                   });
            }
        }

        public async Task SendMessageToPeer(string peerName, string messageContent)
        {
            if (_activePeers.TryGetValue(peerName, out PeerInfo peerInfo))
            {
                if (peerInfo.IsLocal && peerInfo.UserName == _localUserName)
                {
                    Debug.WriteLine($"[NetworkService] Đã cố gắng gửi tin nhắn cho chính mình ({peerName}). Hủy bỏ.");
                    return;
                }

                if (peerInfo.TcpClient == null || !peerInfo.TcpClient.Connected)
                {
                    await ConnectToPeer(peerInfo);
                }

                if (peerInfo.TcpClient != null && peerInfo.TcpClient.Connected)
                {
                    try
                    {
                        var stream = peerInfo.TcpClient.GetStream();
                        Guid messageId = Guid.NewGuid();
                        string messageToSend = $"CHAT:{_localUserName}:{messageId}:{messageContent}";
                        byte[] contentBytes = Encoding.UTF8.GetBytes(messageToSend);
                        byte[] lengthBytes = BitConverter.GetBytes(contentBytes.Length);

                        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                        await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[NetworkService] Lỗi gửi tin nhắn TCP đến {peerName}: {ex.Message}");
                        MarkPeerDisconnected(peerInfo.UserName);
                        throw;
                    }
                }
                else
                {
                    Debug.WriteLine($"[NetworkService] Peer '{peerName}' không thể kết nối được cho tin nhắn TCP.");
                    throw new InvalidOperationException($"Peer '{peerName}' không thể truy cập được.");
                }
            }
            else
            {
                Debug.WriteLine($"[NetworkService] Peer '{peerName}' không tìm thấy cho tin nhắn TCP.");
                throw new InvalidOperationException($"Peer '{peerName}' không tìm thấy hoặc offline.");
            }
        }

        public async Task SendMulticastMessage(string messageContent)
        {
            try
            {
                string message = $"BROADCAST:{_localUserName}:{messageContent}";
                byte[] data = Encoding.UTF8.GetBytes(message);
                await _udpClient.SendAsync(data, data.Length, new IPEndPoint(_multicastAddress, _multicastPort));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkService] Lỗi gửi tin nhắn multicast: {ex.Message}");
                throw;
            }
        }

        public async Task SendTypingStatus(string senderName, bool isTyping, bool isBroadcast, string targetPeer = null)
        {
            string messageType = isTyping ? "TYPING_START" : "TYPING_STOP";
            string messageContent = $"{messageType}:{senderName}";

            if (isBroadcast)
            {
                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(messageContent);
                    await _udpClient.SendAsync(data, data.Length, new IPEndPoint(_multicastAddress, _multicastPort));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi gửi trạng thái đang nhập broadcast: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(targetPeer) && _activePeers.TryGetValue(targetPeer, out PeerInfo peerInfo))
            {
                if (peerInfo.IsLocal && peerInfo.UserName == _localUserName) return;

                if (peerInfo.TcpClient == null || !peerInfo.TcpClient.Connected)
                {
                    await ConnectToPeer(peerInfo);
                }

                if (peerInfo.TcpClient != null && peerInfo.TcpClient.Connected)
                {
                    try
                    {
                        var stream = peerInfo.TcpClient.GetStream();
                        byte[] contentBytes = Encoding.UTF8.GetBytes(messageContent);
                        byte[] lengthBytes = BitConverter.GetBytes(contentBytes.Length);

                        await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                        await stream.WriteAsync(contentBytes, 0, contentBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi gửi trạng thái đang nhập đến {targetPeer}: {ex.Message}");
                        MarkPeerDisconnected(peerInfo.UserName);
                    }
                }
            }
        }

        private async Task ListenForUdpMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync().WithCancellation(cancellationToken);
                    string message = Encoding.UTF8.GetString(result.Buffer, 0, result.Buffer.Length).TrimEnd('\0');
                    ProcessUdpMessage(message, result.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException se)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi Socket trong ListenForUdpMessages: {se.Message} (Mã lỗi: {se.SocketErrorCode})");
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(100, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi trong ListenForUdpMessages: {ex.Message}");
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(100, cancellationToken);
                }
            }
        }

        private void ProcessUdpMessage(string message, IPEndPoint remoteEndPoint)
        {
            var partsGeneral = message.Split(new[] { ':' }, 3);
            string potentialSenderName = "";
            if (partsGeneral.Length > 1) potentialSenderName = partsGeneral[1];
            if (potentialSenderName == _localUserName && !message.StartsWith("HEARTBEAT:"))
            {
                return;
            }
            if (message.StartsWith("HEARTBEAT:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 3 && int.TryParse(parts[2], out int peerTcpPort))
                {
                    string peerName = parts[1];
                    IPEndPoint peerServiceEndPoint = new IPEndPoint(remoteEndPoint.Address, peerTcpPort);
                    HandlePeerHeartbeat(peerName, peerServiceEndPoint);
                }
            }
            else if (message.StartsWith("BROADCAST:"))
            {
                var parts = message.Split(new[] { ':' }, 4);
                if (parts.Length == 4)
                {
                    string senderName = parts[1];
                    Guid messageId = Guid.Parse(parts[2]);
                    string content = parts[3];
                    if (senderName != _localUserName)
                    {
                        OnMessageReceived(new ChatMessage(senderName, content, false, messageId));
                    }
                }
            }
            else if (message.StartsWith("TYPING_START:") || message.StartsWith("TYPING_STOP:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 2)
                {
                    string senderName = parts[1];
                    bool isTyping = message.StartsWith("TYPING_START:");
                    if (senderName != _localUserName)
                    {
                        TypingStatusReceived?.Invoke(this, new TypingStatusEventArgs(senderName, isTyping));
                    }
                }
            }
            else if (message.StartsWith("NAME_UPDATE:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 3)
                {
                    string oldName = parts[1];
                    string newName = parts[2];
                    HandleNameUpdate(oldName, newName, remoteEndPoint);
                }
            }
        }

        private void HandleNameUpdate(string oldName, string newName, IPEndPoint remoteEndPoint)
        {
            if (_activePeers.TryRemove(oldName, out PeerInfo peerInfo))
            {
                var newPeerInfo = new PeerInfo(newName, peerInfo.EndPoint, false)
                {
                    LastHeartbeat = peerInfo.LastHeartbeat
                };
                if (peerInfo.TcpClient != null && peerInfo.TcpClient.Connected)
                {
                    newPeerInfo.SetTcpClient(peerInfo.TcpClient);
                }
                _activePeers.TryAdd(newName, newPeerInfo);
                Debug.WriteLine($"[NetworkService] Đã cập nhật tên peer: {oldName} -> {newName}");
                PeerDisconnected?.Invoke(this, new PeerDiscoveryEventArgs(oldName, peerInfo.EndPoint));
                PeerDiscovered?.Invoke(this, new PeerDiscoveryEventArgs(newName, newPeerInfo.EndPoint));
            }
            else
            {
                Debug.WriteLine($"[NetworkService] Peer {oldName} không tìm thấy để cập nhật thành {newName}. Đang thêm như peer mới.");
                var newPeerInfo = new PeerInfo(newName, remoteEndPoint, false)
                {
                    LastHeartbeat = DateTime.UtcNow
                };
                _activePeers.TryAdd(newName, newPeerInfo);
                PeerDiscovered?.Invoke(this, new PeerDiscoveryEventArgs(newName, newPeerInfo.EndPoint));
            }
        }

        private void HandlePeerHeartbeat(string peerName, IPEndPoint peerServiceEndPoint)
        {
            if (peerName == _localUserName) return;

            _activePeers.AddOrUpdate(
                peerName,
                (key) =>
                {
                    Debug.WriteLine($"[NetworkService] Peer được phát hiện bởi heartbeat: {peerName} tại {peerServiceEndPoint}");
                    PeerDiscovered?.Invoke(this, new PeerDiscoveryEventArgs(peerName, peerServiceEndPoint));
                    return new PeerInfo(peerName, peerServiceEndPoint, false);
                },
                (key, existingPeer) =>
                {
                    existingPeer.LastHeartbeat = DateTime.UtcNow;
                    if (!existingPeer.EndPoint.Equals(peerServiceEndPoint))
                    {
                        Debug.WriteLine($"[NetworkService] Peer {peerName} đã thay đổi endpoint từ {existingPeer.EndPoint} sang {peerServiceEndPoint}. Đang cập nhật.");
                        existingPeer.EndPoint = peerServiceEndPoint;
                        existingPeer.TcpClient?.Close();
                        existingPeer.SetTcpClient(null);
                    }
                    return existingPeer;
                }
            );
        }

        private async Task ListenForTcpConnections(CancellationToken cancellationToken)
        {
            if (_tcpListener == null || !_tcpListener.Server.IsBound)
            {
                Debug.WriteLine("[NetworkService] TCP Listener chưa được khởi động trong ListenForTcpConnections. Đang hủy tác vụ.");
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync(cancellationToken);
                    if (client != null)
                    {
                        Debug.WriteLine($"[NetworkService] Đã chấp nhận kết nối TCP từ {client.Client.RemoteEndPoint}");
                        _ = Task.Run(() => HandleIncomingTcpClient(client, cancellationToken), cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException se) when (se.SocketErrorCode == SocketError.Interrupted || se.SocketErrorCode == SocketError.OperationAborted)
                {
                    Debug.WriteLine("[NetworkService] TCP Listener chấp nhận bị gián đoạn hoặc hủy bỏ, có thể do tắt máy hoặc hủy bỏ.");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi trong ListenForTcpConnections: {ex.Message}");
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(100, cancellationToken);
                }
            }
        }

        private async Task HandleIncomingTcpClient(TcpClient client, CancellationToken cancellationToken)
        {
            IPEndPoint remoteEndPoint = null;
            string associatedPeerName = null;
            HashSet<Guid> processedMessageIds = new HashSet<Guid>();

            try
            {
                remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                Debug.WriteLine($"[NetworkService] Đã chấp nhận kết nối TCP từ {remoteEndPoint}");

                using (client)
                using (var stream = client.GetStream())
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    while (!cancellationToken.IsCancellationRequested && client.Connected)
                    {
                        try
                        {
                            if (stream.DataAvailable)
                            {
                                int length = reader.ReadInt32();
                                byte[] buffer = reader.ReadBytes(length);
                                string message = Encoding.UTF8.GetString(buffer);

                                var parts = message.Split(new[] { ':' }, 4);
                                if (parts.Length == 4 && parts[0] == "CHAT")
                                {
                                    string senderName = parts[1];
                                    if (Guid.TryParse(parts[2], out Guid messageId))
                                    {
                                        string content = parts[3];
                                        if (!processedMessageIds.Contains(messageId))
                                        {
                                            Debug.WriteLine($"[NetworkService] Đã nhận tin nhắn từ {remoteEndPoint}: {message}");
                                            ProcessTcpMessage(message, remoteEndPoint, client, associatedPeerName);
                                            processedMessageIds.Add(messageId);
                                        }
                                        else
                                        {
                                            Debug.WriteLine($"[NetworkService] Đã bỏ qua tin nhắn trùng lặp (MessageId: {messageId}) từ {remoteEndPoint}");
                                        }
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[NetworkService] MessageId không hợp lệ: {parts[2]}");
                                    }
                                }
                                else
                                {
                                    Debug.WriteLine($"[NetworkService] Định dạng tin nhắn TCP không hợp lệ từ {remoteEndPoint}: {message}");
                                }

                                if (associatedPeerName == null && message.StartsWith("USERNAME:"))
                                {
                                    associatedPeerName = message.Substring("USERNAME:".Length);
                                    Debug.WriteLine($"[NetworkService] Tên peer liên kết cho {remoteEndPoint}: {associatedPeerName}");
                                }
                            }
                            await Task.Delay(100, cancellationToken);
                        }
                        catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            Debug.WriteLine($"[NetworkService] Client đã ngắt kết nối: {ex.Message}");
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[NetworkService] Lỗi xử lý client TCP từ {remoteEndPoint}: {ex.Message}");
                            break;
                        }
                    }
                }
            }
            finally
            {
                Debug.WriteLine($"[NetworkService] Đã đóng kết nối TCP với {remoteEndPoint} (Peer: {associatedPeerName})");
            }
        }

        private string ProcessTcpMessage(string message, IPEndPoint remoteEndPoint, TcpClient client, string currentAssociatedPeerName)
        {
            string identifiedSenderName = currentAssociatedPeerName;

            if (message.StartsWith("CHAT:"))
            {
                var parts = message.Split(new[] { ':' }, 4);
                if (parts.Length == 4)
                {
                    string senderName = parts[1];
                    identifiedSenderName = senderName;
                    string content = parts[3];
                    OnMessageReceived(new ChatMessage(senderName, content, false));

                    if (_activePeers.TryGetValue(senderName, out PeerInfo peerInfo))
                    {
                        if (peerInfo.TcpClient != client)
                        {
                            peerInfo.SetTcpClient(client);
                            Debug.WriteLine($"[NetworkService] Đã liên kết client TCP từ {remoteEndPoint} với peer {senderName}.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[NetworkService] Đã nhận TCP CHAT từ peer không xác định {senderName} tại {remoteEndPoint}. Tin nhắn: {content}");
                    }
                }
            }
            else if (message.StartsWith("HELLO:"))
            {
                var parts = message.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    string senderName = parts[1];
                    identifiedSenderName = senderName;
                    Debug.WriteLine($"[NetworkService] Đã nhận HELLO từ {senderName} qua TCP từ {remoteEndPoint}.");
                    if (_activePeers.TryGetValue(senderName, out PeerInfo peerInfo))
                    {
                        peerInfo.LastHeartbeat = DateTime.UtcNow;
                        if (peerInfo.TcpClient != client)
                        {
                            peerInfo.SetTcpClient(client);
                            Debug.WriteLine($"[NetworkService] Đã liên kết client TCP (qua HELLO) từ {remoteEndPoint} với peer {senderName}.");
                        }
                    }
                    else
                    {
                        var newPeerInfo = new PeerInfo(senderName, new IPEndPoint(remoteEndPoint.Address, 0), false);
                        newPeerInfo.SetTcpClient(client);
                        _activePeers.AddOrUpdate(senderName, newPeerInfo, (k, existing) =>
                        {
                            existing.SetTcpClient(client);
                            existing.LastHeartbeat = DateTime.UtcNow;
                            return existing;
                        });
                        PeerDiscovered?.Invoke(this, new PeerDiscoveryEventArgs(senderName, newPeerInfo.EndPoint));
                        Debug.WriteLine($"[NetworkService] Peer {senderName} được phát hiện qua TCP HELLO từ {remoteEndPoint}. Đã thêm/cập nhật active peers.");
                    }
                }
            }
            else if (message.StartsWith("TYPING_START:") || message.StartsWith("TYPING_STOP:"))
            {
                var parts = message.Split(':');
                if (parts.Length == 2)
                {
                    string senderName = parts[1];
                    identifiedSenderName = senderName;
                    bool isTyping = message.StartsWith("TYPING_START:");
                    if (senderName != _localUserName)
                    {
                        TypingStatusReceived?.Invoke(this, new TypingStatusEventArgs(senderName, isTyping));
                    }
                }
            }
            return identifiedSenderName;
        }

        private async Task ConnectToPeer(PeerInfo peerInfo)
        {
            if (peerInfo.IsLocal || (peerInfo.TcpClient != null && peerInfo.TcpClient.Connected))
                return;

            Debug.WriteLine($"[NetworkService] Đang cố gắng kết nối đến peer {peerInfo.UserName} tại {peerInfo.EndPoint}");
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                var connectTask = client.ConnectAsync(peerInfo.EndPoint.Address, peerInfo.EndPoint.Port);
                var timeoutTask = Task.Delay(3000, _cancellationTokenSource.Token);

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask && !connectTask.IsFaulted)
                {
                    if (client.Connected)
                    {
                        peerInfo.SetTcpClient(client);
                        Debug.WriteLine($"[NetworkService] Đã kết nối thành công đến peer {peerInfo.UserName}. Đang gửi HELLO.");
                        await SendTcpHelloMessage(peerInfo);
                    }
                    else
                    {
                        client.Close();
                        Debug.WriteLine($"[NetworkService] Không thể kết nối đến peer {peerInfo.UserName} (đã hoàn thành nhưng không kết nối).");
                    }
                }
                else
                {
                    client.Close();
                    if (connectTask.IsFaulted)
                    {
                        Debug.WriteLine($"[NetworkService] Không thể kết nối đến peer {peerInfo.UserName} (lỗi: {connectTask.Exception?.InnerException?.Message}).");
                    }
                    else
                    {
                        Debug.WriteLine($"[NetworkService] Không thể kết nối đến peer {peerInfo.UserName} (hết thời gian chờ).");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                client?.Close();
                Debug.WriteLine($"[NetworkService] Nỗ lực kết nối đến {peerInfo.UserName} đã bị hủy.");
            }
            catch (SocketException ex)
            {
                client?.Close();
                Debug.WriteLine($"[NetworkService] Lỗi Socket khi kết nối đến peer {peerInfo.UserName} tại {peerInfo.EndPoint}: {ex.Message}");
                peerInfo.SetTcpClient(null);
            }
            catch (Exception ex)
            {
                client?.Close();
                Debug.WriteLine($"[NetworkService] Lỗi chung khi kết nối đến peer {peerInfo.UserName}: {ex.Message}");
                peerInfo.SetTcpClient(null);
            }
        }

        private async Task SendTcpHelloMessage(PeerInfo peerInfo)
        {
            if (peerInfo.TcpClient != null && peerInfo.TcpClient.Connected)
            {
                try
                {
                    var stream = peerInfo.TcpClient.GetStream();
                    string message = $"HELLO:{_localUserName}";
                    byte[] contentBytes = Encoding.UTF8.GetBytes(message);
                    byte[] lengthBytes = BitConverter.GetBytes(contentBytes.Length);

                    await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length, _cancellationTokenSource.Token);
                    await stream.WriteAsync(contentBytes, 0, contentBytes.Length, _cancellationTokenSource.Token);
                    Debug.WriteLine($"[NetworkService] Đã gửi HELLO đến {peerInfo.UserName}");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"[NetworkService] Việc gửi HELLO đến {peerInfo.UserName} đã bị hủy.");
                    peerInfo.TcpClient.Close(); peerInfo.SetTcpClient(null);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi khi gửi HELLO đến {peerInfo.UserName}: {ex.Message}");
                    peerInfo.TcpClient.Close();
                    peerInfo.SetTcpClient(null);
                }
            }
        }

        private async Task SendHeartbeatPeriodically(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string heartbeatMessage = $"HEARTBEAT:{_localUserName}:{_tcpPort}";
                    byte[] data = Encoding.UTF8.GetBytes(heartbeatMessage);
                    await _udpClient.SendAsync(data, data.Length, new IPEndPoint(_multicastAddress, _multicastPort));
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi trong SendHeartbeatPeriodically: {ex.Message}");
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task CleanupDisconnectedPeers(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var peersToRemove = new List<string>();

                    foreach (var entry in _activePeers.ToList())
                    {
                        var peerName = entry.Key;
                        var peerInfo = entry.Value;

                        if (peerInfo.IsLocal && peerInfo.UserName == _localUserName) continue;

                        if ((now - peerInfo.LastHeartbeat).TotalSeconds > 15)
                        {
                            peersToRemove.Add(peerName);
                        }
                    }

                    foreach (var peerNameToRemove in peersToRemove)
                    {
                        MarkPeerDisconnected(peerNameToRemove);
                    }
                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi trong CleanupDisconnectedPeers: {ex.Message}");
                    if (!cancellationToken.IsCancellationRequested) await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private void MarkPeerDisconnected(string peerName)
        {
            if (_activePeers.TryRemove(peerName, out PeerInfo removedPeer))
            {
                removedPeer.Dispose();
                Debug.WriteLine($"[NetworkService] Peer {peerName} đã được đánh dấu là ngắt kết nối và đã xóa. Endpoint: {removedPeer.EndPoint}");
                PeerDisconnected?.Invoke(this, new PeerDiscoveryEventArgs(peerName, removedPeer.EndPoint));
            }
        }

        protected virtual void OnMessageReceived(ChatMessage message)
        {
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }

        public IEnumerable<string> GetActivePeerNames()
        {
            return _activePeers.Keys
                .Where(name => name != _localUserName && (!_activePeers.TryGetValue(name, out var peerInfo) || !peerInfo.IsLocal))
                .OrderBy(n => n)
                .ToList();
        }

        public void Dispose()
        {
            Debug.WriteLine("[NetworkService] Đang giải phóng...");
            _cancellationTokenSource?.Cancel();

            try
            {
                _tcpListener?.Stop();
                Debug.WriteLine("[NetworkService] TCP Listener đã dừng.");

                Task[] tasks = { _udpListenTask, _tcpListenTask, _heartbeatTask, _peerCleanupTask };
                var runningTasks = tasks.Where(t => t != null && !t.IsCompleted && !t.IsCanceled && !t.IsFaulted).ToArray();
                if (runningTasks.Any())
                {
                    Task.WaitAll(runningTasks, TimeSpan.FromSeconds(2));
                }
                Debug.WriteLine("[NetworkService] Các tác vụ nền đã được chờ hoặc đã hoàn thành.");
            }
            catch (AggregateException ex)
            {
                foreach (var innerEx in ex.InnerExceptions)
                {
                    Debug.WriteLine($"[NetworkService] Lỗi trong quá trình tắt tác vụ: {innerEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkService] Lỗi chung trong quá trình tắt máy: {ex.Message}");
            }
            finally
            {
                if (_udpClient != null)
                {
                    try
                    {
                        _udpClient.Close();
                        _udpClient.Dispose();
                        Debug.WriteLine("[NetworkService] UDP Client đã đóng và giải phóng.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[NetworkService] Lỗi khi đóng/giải phóng UDP client: {ex.Message}");
                    }
                }

                foreach (var peer in _activePeers.Values)
                {
                    peer.Dispose();
                }
                _activePeers.Clear();
                Debug.WriteLine("[NetworkService] Các kết nối peer đang hoạt động đã được giải phóng và danh sách đã được xóa.");

                _cancellationTokenSource?.Dispose();
                Debug.WriteLine("[NetworkService] Giải phóng hoàn tất.");
            }
        }
    }

    public class PeerInfo : IDisposable
    {
        public string UserName { get; }
        public IPEndPoint EndPoint { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public TcpClient TcpClient { get; private set; }
        public bool IsLocal { get; }

        public PeerInfo(string userName, IPEndPoint endPoint, bool isLocal = false)
        {
            UserName = userName;
            EndPoint = endPoint;
            LastHeartbeat = DateTime.UtcNow;
            IsLocal = isLocal;
        }

        public void SetTcpClient(TcpClient client)
        {
            if (TcpClient != null && TcpClient != client)
            {
                try
                {
                    TcpClient.Close();
                    TcpClient.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PeerInfo] Lỗi khi giải phóng TcpClient cũ cho {UserName}: {ex.Message}");
                }
            }
            TcpClient = client;
        }

        public void Dispose()
        {
            try
            {
                TcpClient?.Close();
                TcpClient?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PeerInfo] Lỗi khi giải phóng TcpClient cho {UserName} trong PeerInfo.Dispose: {ex.Message}");
            }
            TcpClient = null;
        }
    }
}