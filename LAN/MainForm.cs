using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Diagnostics;
using Messenger.Properties;

namespace Messenger
{
    public partial class MainForm : Form
    {
        private NetworkService _networkService;
        private List<ChatMessage> _chatMessages = new List<ChatMessage>();
        private BindingList<string> _onlineUsers = new BindingList<string>();
        private const int TcpPort = 14000;
        private const string MulticastAddress = "239.255.0.1";
        private const int MulticastPort = 14001;
        private Image _myAvatar;
        private string _myUserName;
        private Random _random = new Random();
        private System.Windows.Forms.Timer _typingTimer = new System.Windows.Forms.Timer();
        private System.Windows.Forms.Timer _dateTimeTimer;
        private bool _isTyping = false;
        private string _remoteTypingUser = "";
        private Dictionary<string, Image> _userAvatars = new Dictionary<string, Image>();
        private string _profileDirectory;
        private string _userNameFilePath;
        private string _chatHistoryDirectory;
        private string _currentPeer;
        private NotifyIcon _trayIcon;
        private bool _isClosing = false;
        private const int AvatarWidth = 40;
        private const int AvatarHeight = 40;
        private const int AvatarPadding = 5;
        private System.Windows.Forms.Timer _rainbowTimer;
        private float _rainbowPhase = 0f;
        private readonly float _rainbowSpeed = 0.05f; // Tốc độ thay đổi màu

        public MainForm()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            chatListBox.DrawMode = DrawMode.OwnerDrawVariable;
            chatListBox.DrawItem += ChatListBox_DrawItem;
            chatListBox.MeasureItem += ChatListBox_MeasureItem;
            chatListBox.MouseDown += ChatListBox_MouseDown;
            chatListBox.MouseClick += ChatListBox_MouseClick;
            chatListBox.MouseMove += ChatListBox_MouseMove;

            messageTextBox.Enter += (s, e) => RemovePlaceholder();
            messageTextBox.Leave += (s, e) => SetPlaceholder();
            SetPlaceholder();

            chatListBox.ContextMenuStrip = messageContextMenuStrip;
            copyToolStripMenuItem.Click += copyToolStripMenuItem_Click;

            MessageRenderer.Initialize(chatListBox.Font);

            onlineUsersListBox.DataSource = _onlineUsers;
            onlineUsersListBox.SelectedIndexChanged += OnlineUsersListBox_SelectedIndexChanged;

            messageTextBox.KeyDown += messageTextBox_KeyDown;
            messageTextBox.TextChanged += messageTextBox_TextChanged;

            _typingTimer.Interval = 1000;
            _typingTimer.Tick += _typingTimer_Tick;

            _dateTimeTimer = new System.Windows.Forms.Timer();
            _dateTimeTimer.Interval = 500;
            _dateTimeTimer.Tick += DateTimeTimer_Tick;
            _dateTimeTimer.Start();

            _rainbowTimer = new System.Windows.Forms.Timer();
            _rainbowTimer.Interval = 50; // Cập nhật màu mỗi 50ms để mượt mà
            _rainbowTimer.Tick += RainbowTimer_Tick;
            authorLabel.MouseEnter += AuthorLabel_MouseEnter;
            authorLabel.MouseLeave += AuthorLabel_MouseLeave;

            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;

            _profileDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LAN Messenger");
            _userNameFilePath = Path.Combine(_profileDirectory, "user_profile.txt");
            _chatHistoryDirectory = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "log");
            Directory.CreateDirectory(_profileDirectory);
            Directory.CreateDirectory(_chatHistoryDirectory);

            InitializeSystemTray();
        }
        private string GetVietnameseDayOfWeek(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: return "(Thứ Hai)";
                case DayOfWeek.Tuesday: return "(Thứ Ba)";
                case DayOfWeek.Wednesday: return "(Thứ Tư)";
                case DayOfWeek.Thursday: return "(Thứ Năm)";
                case DayOfWeek.Friday: return "(Thứ Sáu)";
                case DayOfWeek.Saturday: return "(Thứ Bảy)";
                case DayOfWeek.Sunday: return "(Chủ Nhật)";
                default: return "(Không xác định)";
            }
        }
        private void DateTimeTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                TimeZoneInfo gmtPlus7 = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                DateTime gmtPlus7Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, gmtPlus7);
                timeLabel.Text = gmtPlus7Time.ToString("HH:mm:ss");
                dateLabel.Text = gmtPlus7Time.ToString("dd/MM/yyyy");
                dayLabel.Text = GetVietnameseDayOfWeek(gmtPlus7Time.DayOfWeek);
            }
            catch (TimeZoneNotFoundException)
            {
                Debug.WriteLine("Múi giờ SE Asia Standard Time không tìm thấy, sử dụng thời gian hệ thống.");
                timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
                dateLabel.Text = DateTime.Now.ToString("dd/MM/yyyy");
                dayLabel.Text = GetVietnameseDayOfWeek(DateTime.Now.DayOfWeek);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi lấy thời gian GMT+7: {ex.Message}");
                timeLabel.Text = "Lỗi thời gian";
                dateLabel.Text = "Lỗi ngày";
                dayLabel.Text = "Lỗi thứ";
            }
        }

        private void RainbowTimer_Tick(object sender, EventArgs e)
        {
            // Tăng phase để tạo hiệu ứng chuyển màu
            _rainbowPhase += _rainbowSpeed;
            if (_rainbowPhase >= 2 * Math.PI) _rainbowPhase -= (float)(2 * Math.PI);

            // Tính toán màu sắc dựa trên hàm sin
            int r = (int)(Math.Sin(_rainbowPhase) * 127 + 128);
            int g = (int)(Math.Sin(_rainbowPhase + 2 * Math.PI / 3) * 127 + 128);
            int b = (int)(Math.Sin(_rainbowPhase + 4 * Math.PI / 3) * 127 + 128);

            // Cập nhật màu của authorLabel
            authorLabel.ForeColor = Color.FromArgb(r, g, b);
        }

        private void AuthorLabel_MouseEnter(object sender, EventArgs e)
        {
            _rainbowTimer.Start();
        }

        private void AuthorLabel_MouseLeave(object sender, EventArgs e)
        {
            _rainbowTimer.Stop();
            authorLabel.ForeColor = Color.Black; // Khôi phục màu mặc định
        }

        private void InitializeSystemTray()
        {
            _trayIcon = new NotifyIcon
            {
                Text = "LAN Messenger",
                Visible = true
            };

            // Gán biểu tượng của MainForm cho khay hệ thống
            if (this.Icon != null)
            {
                _trayIcon.Icon = this.Icon;
                Debug.WriteLine("Đã gán biểu tượng của MainForm cho khay hệ thống.");
            }
            else
            {
                _trayIcon.Icon = SystemIcons.Application;
                Debug.WriteLine("Không tìm thấy biểu tượng của MainForm. Sử dụng biểu tượng mặc định SystemIcons.Application. Vui lòng kiểm tra thiết lập biểu tượng trong Properties của dự án hoặc MainForm.");
            }

            // Tạo menu ngữ cảnh cho biểu tượng khay
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Mở", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add("Thoát", null, (s, e) => ExitApplication());
            _trayIcon.ContextMenuStrip = trayMenu;

            // Xử lý nhấp đúp để khôi phục
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized && !_isClosing)
            {
                this.Hide();
                _trayIcon.Visible = true;
                if (_trayIcon.Icon != null)
                {
                    var balloon = new CustomBalloonForm(
                        "LAN Messenger",
                        "Ứng dụng đã được thu nhỏ vào khay hệ thống. Nhấp đúp để khôi phục.",
                        this.Icon,
                        3000,
                        RestoreFromTray
                    );
                    balloon.Show();
                    Debug.WriteLine("Hiển thị thông báo tùy chỉnh khi thu nhỏ.");
                }
                else
                {
                    Debug.WriteLine("Không hiển thị thông báo tùy chỉnh vì _trayIcon.Icon là null.");
                }
            }
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            _trayIcon.Visible = false; // Ẩn biểu tượng khay khi khôi phục
        }

        private void ExitApplication()
        {
            _isClosing = true;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        private string LoadUserName()
        {
            if (File.Exists(_userNameFilePath))
            {
                try
                {
                    string savedName = File.ReadAllText(_userNameFilePath).Trim();
                    if (!string.IsNullOrWhiteSpace(savedName) && !savedName.Contains(":"))
                    {
                        return savedName;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi tải tên người dùng: {ex.Message}");
                }
            }
            return null;
        }

        private void SaveUserName(string userName)
        {
            try
            {
                File.WriteAllText(_userNameFilePath, userName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi lưu tên người dùng: {ex.Message}");
            }
        }

        private void SaveChatHistory(string peerName, ChatMessage message)
        {
            try
            {
                string fileName = $"chat_history_{peerName}_{DateTime.Today:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_chatHistoryDirectory, fileName);
                string line = $"[{message.Timestamp:yyyy-MM-dd HH:mm:ss}] {message.SenderName}: {message.Content}";

                File.AppendAllText(filePath, line + Environment.NewLine);

                CleanupOldChatHistory(peerName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi lưu lịch sử chat: {ex.Message}");
            }
        }

        private void UpdateChatHistoryName(string oldName, string newName)
        {
            try
            {
                var files = Directory.GetFiles(_chatHistoryDirectory, $"chat_history_{oldName}_*.txt");
                foreach (var file in files)
                {
                    try
                    {
                        string newFileName = file.Replace($"chat_history_{oldName}_", $"chat_history_{newName}_");
                        File.Move(file, newFileName);
                        Debug.WriteLine($"Đã đổi tên file lịch sử chat: {file} -> {newFileName}");

                        // Cập nhật nội dung file
                        string content = File.ReadAllText(newFileName);
                        content = content.Replace($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {oldName}:", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {newName}:");
                        File.WriteAllText(newFileName, content);
                        Debug.WriteLine($"Đã cập nhật nội dung file lịch sử chat: {newFileName}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi khi cập nhật file lịch sử chat {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi khi cập nhật lịch sử chat cho {oldName} -> {newName}: {ex.Message}");
            }
        }

        private void CleanupOldChatHistory(string peerName)
        {
            try
            {
                var files = Directory.GetFiles(_chatHistoryDirectory, $"chat_history_{peerName}_*.txt")
                    .Select(f => new { Path = f, Date = ParseDateFromFileName(f) })
                    .Where(f => f.Date != null)
                    .OrderByDescending(f => f.Date)
                    .ToList();

                var filesToDelete = files.Skip(10);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file.Path);
                        Debug.WriteLine($"Đã xóa file lịch sử cũ: {file.Path}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Lỗi xóa file lịch sử {file.Path}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi dọn dẹp lịch sử chat: {ex.Message}");
            }
        }

        private DateTime? ParseDateFromFileName(string filePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string datePart = fileName.Split('_').Last();
                if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    return date;
                }
            }
            catch
            {
            }
            return null;
        }

        private void LoadChatHistory(string peerName)
        {
            try
            {
                if (_currentPeer != peerName)
                {
                    _chatMessages.Clear();
                    chatListBox.Items.Clear();
                }

                string fileName = $"chat_history_{peerName}_{DateTime.Today:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(_chatHistoryDirectory, fileName);

                if (File.Exists(filePath))
                {
                    var lines = File.ReadAllLines(filePath);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.Length > 19 && line[0] == '[')
                        {
                            int firstColon = line.IndexOf(':', 19);
                            if (firstColon > 0 && DateTime.TryParse(line.Substring(1, 19), out DateTime timestamp))
                            {
                                string rest = line.Substring(firstColon + 1).Trim();
                                int senderEnd = rest.IndexOf(':');
                                if (senderEnd > 0)
                                {
                                    string sender = rest.Substring(0, senderEnd).Trim();
                                    string content = rest.Substring(senderEnd + 1).Trim();
                                    bool isMyMessage = sender == _myUserName;
                                    var message = new ChatMessage(sender, content, isMyMessage);
                                    message.Timestamp = timestamp;
                                    if (!_chatMessages.Any(m => m.SenderName == sender && m.Content == content && m.Timestamp == timestamp))
                                    {
                                        AddMessageToChat(message);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi tải lịch sử chat: {ex.Message}");
            }
        }

        private void SetPlaceholder()
        {
            if (string.IsNullOrEmpty(messageTextBox.Text) && !messageTextBox.Focused)
            {
                messageTextBox.ForeColor = Color.Gray;
                messageTextBox.Text = "Nhập tin nhắn...";
            }
        }

        private void RemovePlaceholder()
        {
            if (messageTextBox.Text == "Nhập tin nhắn..." && messageTextBox.ForeColor == Color.Gray)
            {
                messageTextBox.Text = "";
                messageTextBox.ForeColor = Color.Black;
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (chatListBox.SelectedItem is ChatMessage selectedMessage)
            {
                try
                {
                    Clipboard.SetText(selectedMessage.Content);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể sao chép: {ex.Message}", "Lỗi Sao Chép", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ChatListBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                int index = chatListBox.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    chatListBox.SelectedIndex = index;
                }
                else
                {
                    chatListBox.SelectedIndex = -1;
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _myUserName = LoadUserName();

            if (string.IsNullOrWhiteSpace(_myUserName))
            {
                _myUserName = Environment.UserName;
                if (string.IsNullOrWhiteSpace(_myUserName))
                {
                    _myUserName = "User" + _random.Next(100, 1000);
                }
                SaveUserName(_myUserName);
            }

            // Gán avatar mặc định cho người dùng hiện tại
            try
            {
                _myAvatar = Properties.Resources.default_avatar;
                Debug.WriteLine("Đã gán default_avatar.png cho _myAvatar.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi tải default_avatar.png: {ex.Message}");
                _myAvatar = SystemIcons.Question.ToBitmap();
            }

            this.Text = $"LAN Messenger - {_myUserName}";

            try
            {
                _networkService = new NetworkService(_myUserName, TcpPort, MulticastAddress, MulticastPort);
                _networkService.MessageReceived += NetworkService_MessageReceived;
                _networkService.PeerDiscovered += NetworkService_PeerDiscovered;
                _networkService.PeerDisconnected += NetworkService_PeerDisconnected;
                _networkService.TypingStatusReceived += NetworkService_TypingStatusReceived;

                _networkService.Start();
                UpdateOnlineUsersList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể khởi động dịch vụ mạng.\nLỗi: {ex.Message}", "Lỗi Khởi Động Mạng", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ExitApplication();
            }
        }

        private async void btnRename_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("btnRename_Click triggered");

            using (RenameForm renameForm = new RenameForm(_myUserName))
            {
                if (renameForm.ShowDialog(this) == DialogResult.OK)
                {
                    string newName = renameForm.NewUserName;

                    if (!string.IsNullOrWhiteSpace(newName) && newName != _myUserName)
                    {
                        if (newName.Contains(":"))
                        {
                            MessageBox.Show("Tên không được chứa ký tự ':' vì nó được sử dụng làm dấu phân cách trong giao thức mạng.", "Tên không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        try
                        {
                            string oldName = _myUserName;
                            _myUserName = newName;
                            SaveUserName(_myUserName);
                            this.Text = $"LAN Messenger - {_myUserName}";

                            if (_networkService != null)
                            {
                                _networkService.UpdateLocalUserName(_myUserName);
                                await _networkService.SendNameUpdate(oldName, _myUserName);

                                // Cập nhật tin nhắn và lịch sử chat
                                UpdateChatMessagesName(oldName, _myUserName);
                                UpdateChatHistoryName(oldName, _myUserName);

                                // Cập nhật peer hiện tại nếu cần
                                if (_currentPeer == oldName)
                                {
                                    _currentPeer = _myUserName;
                                    selectedPeerLabel.Text = $"Đang chat với: {_myUserName}";
                                    selectedPeerLabel.Tag = _myUserName;
                                    LoadChatHistory(_myUserName);
                                }

                                UpdateOnlineUsersList();

                                // Thêm thông báo hệ thống
                                AddMessageToChat(new ChatMessage("Hệ thống", $"Bạn đã đổi tên từ {oldName} thành {newName}", false, ChatMessage.MessageType.System));
                            }

                            MessageBox.Show($"Tên của bạn đã được đổi thành: {newName}", "Đổi tên thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        catch (InvalidOperationException ex)
                        {
                            MessageBox.Show(ex.Message, "Lỗi Đổi Tên", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Lỗi gửi thông báo đổi tên: {ex.Message}");
                            MessageBox.Show($"Không thể thông báo đổi tên đến các peer khác: {ex.Message}", "Lỗi Đổi Tên", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
        }

        private void UpdateChatMessagesName(string oldName, string newName)
        {
            if (!_chatMessages.Any())
            {
                Debug.WriteLine("Không có tin nhắn để cập nhật tên.");
                return;
            }

            foreach (var message in _chatMessages)
            {
                if (message.SenderName == oldName)
                {
                    message.SenderName = newName;
                    message.IsMyMessage = newName == _myUserName;
                }
            }
            chatListBox.Items.Clear();
            foreach (var message in _chatMessages)
            {
                chatListBox.Items.Add(message);
            }
            if (chatListBox.Items.Count > 0)
            {
                chatListBox.TopIndex = chatListBox.Items.Count - 1;
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isClosing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
                this.Hide();
                _trayIcon.Visible = true;
                if (_trayIcon.Icon != null)
                {
                    var balloon = new CustomBalloonForm(
                        "LAN Messenger",
                        "Ứng dụng đã được thu nhỏ vào khay hệ thống. Nhấp đúp để khôi phục.",
                        this.Icon,
                        5000,
                        RestoreFromTray
                    );
                    balloon.Show();
                    Debug.WriteLine("Hiển thị thông báo tùy chỉnh khi đóng.");
                }
                else
                {
                    Debug.WriteLine("Không hiển thị thông báo tùy chỉnh vì _trayIcon.Icon là null.");
                }
            }
            else
            {
                _dateTimeTimer?.Stop();
                _dateTimeTimer?.Dispose();
                _rainbowTimer?.Stop();
                _rainbowTimer?.Dispose();
                try
                {
                    _networkService?.Dispose();
                    _trayIcon?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi khi đóng dịch vụ mạng: {ex.Message}");
                }
            }
        }

        private void NetworkService_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => NetworkService_MessageReceived(sender, e)));
                return;
            }

            string selectedPeer = selectedPeerLabel.Tag as string ?? "Broadcast";
            if (e.Message.SenderName == selectedPeer || selectedPeer == "Broadcast")
            {
                AddMessageToChat(e.Message);
                SaveChatHistory(selectedPeer, e.Message);

                // Hiển thị thông báo nếu ứng dụng đang thu nhỏ
                if (this.WindowState == FormWindowState.Minimized || !this.Visible)
                {
                    string notificationText = e.Message.Content.Length > 50
                        ? e.Message.Content.Substring(0, 47) + "..."
                        : e.Message.Content;
                    if (_trayIcon.Icon != null)
                    {
                        var balloon = new CustomBalloonForm(
                            $"Tin nhắn mới từ {e.Message.SenderName}",
                            notificationText,
                            this.Icon,
                            5000,
                            () =>
                            {
                                RestoreFromTray();
                                if (_onlineUsers.Contains(e.Message.SenderName))
                                {
                                    onlineUsersListBox.SelectedItem = e.Message.SenderName;
                                }
                            }
                        );
                        balloon.Show();
                        Debug.WriteLine($"Hiển thị thông báo tùy chỉnh cho tin nhắn từ {e.Message.SenderName}.");
                    }
                    else
                    {
                        Debug.WriteLine("Không hiển thị thông báo tùy chỉnh vì _trayIcon.Icon là null.");
                    }
                }
            }
        }

        private void NetworkService_PeerDiscovered(object sender, PeerDiscoveryEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => HandlePeerDiscovered(e)));
            }
            else
            {
                HandlePeerDiscovered(e);
            }
        }

        private void NetworkService_PeerDisconnected(object sender, PeerDiscoveryEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => HandlePeerDisconnected(e)));
            }
            else
            {
                HandlePeerDisconnected(e);
            }
        }

        private void HandlePeerDiscovered(PeerDiscoveryEventArgs e)
        {
            UpdateOnlineUsersList();
            if (_chatMessages.Any(m => m.SenderName == e.PeerName))
            {
                LoadChatHistory(e.PeerName);
            }
        }

        private void HandlePeerDisconnected(PeerDiscoveryEventArgs e)
        {
            UpdateOnlineUsersList();
            string newName = _networkService.GetActivePeerNames().FirstOrDefault(n => !_onlineUsers.Contains(e.PeerName) && n != e.PeerName);
            if (!string.IsNullOrEmpty(newName))
            {
                UpdateChatMessagesName(e.PeerName, newName);
                UpdateChatHistoryName(e.PeerName, newName);
                if (_currentPeer == e.PeerName)
                {
                    _currentPeer = newName;
                    selectedPeerLabel.Text = $"Đang chat với: {newName}";
                    selectedPeerLabel.Tag = newName;
                    LoadChatHistory(newName);
                }
                AddMessageToChat(new ChatMessage("Hệ thống", $"{e.PeerName} đã đổi tên thành {newName}", false, ChatMessage.MessageType.System));
            }
        }

        private void NetworkService_TypingStatusReceived(object sender, TypingStatusEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateRemoteTypingStatus(e.SenderName, e.IsTyping)));
            }
            else
            {
                UpdateRemoteTypingStatus(e.SenderName, e.IsTyping);
            }
        }

        private void UpdateRemoteTypingStatus(string senderName, bool isTyping)
        {
            string selectedPeer = selectedPeerLabel.Tag as string;
            if (selectedPeer != null && selectedPeer == senderName)
            {
                if (isTyping)
                {
                    _remoteTypingUser = senderName;
                    _typingStatusLabel.Text = $"{senderName} đang nhập tin nhắn...";
                    _typingStatusLabel.BackColor = Color.Transparent;
                }
                else
                {
                    if (_remoteTypingUser == senderName)
                    {
                        _remoteTypingUser = "";
                        _typingStatusLabel.Text = "";
                    }
                }
            }
            else if (selectedPeer == "Broadcast" && isTyping)
            {
                _remoteTypingUser = senderName;
                _typingStatusLabel.Text = $"{senderName} đang nhập tin nhắn...";
            }
            else if (selectedPeer == "Broadcast" && !isTyping && _remoteTypingUser == senderName)
            {
                _remoteTypingUser = "";
                _typingStatusLabel.Text = "";
            }
        }

        private void messageTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!_isTyping && messageTextBox.Text.Length > 0 && messageTextBox.ForeColor == Color.Black)
            {
                _isTyping = true;
                SendTypingStatus(true);
            }
            else if (_isTyping && messageTextBox.Text.Length == 0)
            {
                _isTyping = false;
                SendTypingStatus(false);
            }
            _typingTimer.Stop();
            if (messageTextBox.Text.Length > 0 && messageTextBox.ForeColor == Color.Black)
            {
                _typingTimer.Start();
            }
        }

        private void _typingTimer_Tick(object sender, EventArgs e)
        {
            _typingTimer.Stop();
            _isTyping = false;
            SendTypingStatus(false);
        }

        private async void SendTypingStatus(bool isTyping)
        {
            string selectedPeer = selectedPeerLabel.Tag as string;
            if (string.IsNullOrEmpty(selectedPeer))
            {
                selectedPeer = "Broadcast";
            }

            try
            {
                if (selectedPeer == _myUserName) return;

                if (selectedPeer == "Broadcast")
                {
                    await _networkService.SendTypingStatus(_myUserName, isTyping, true);
                }
                else
                {
                    await _networkService.SendTypingStatus(_myUserName, isTyping, false, selectedPeer);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi gửi trạng thái typing: {ex.Message}");
            }
        }

        private void AddMessageToChat(ChatMessage message)
        {
            using (Graphics g = chatListBox.CreateGraphics())
            {
                MessageRenderer.PrepareMessageForDrawing(message, g, chatListBox.Width, chatListBox.Font);
            }

            if (message.IsMyMessage)
            {
                message.Avatar = _myAvatar;
            }
            else
            {
                message.Avatar = GetAvatarForUser(message.SenderName);
            }

            _chatMessages.Add(message);
            chatListBox.Items.Add(message);
            if (chatListBox.Items.Count > 0)
            {
                chatListBox.TopIndex = chatListBox.Items.Count - 1;
            }
        }

        private Image GetAvatarForUser(string senderName)
        {
            try
            {
                if (_userAvatars.ContainsKey(senderName))
                {
                    return _userAvatars[senderName];
                }
                else
                {
                    return Properties.Resources.other_default_avatar;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi tải other_default_avatar.png cho {senderName}: {ex.Message}");
                return SystemIcons.Question.ToBitmap();
            }
        }

        private void UpdateOnlineUsersList()
        {
            var currentPeers = _networkService.GetActivePeerNames().ToList();
            string currentSelectedPeer = _currentPeer;

            _onlineUsers.Clear();
            _onlineUsers.Add("Broadcast");

            foreach (var peerName in currentPeers.OrderBy(p => p))
            {
                if (peerName != _myUserName)
                {
                    _onlineUsers.Add(peerName);
                }
            }

            if (!string.IsNullOrEmpty(currentSelectedPeer) && _onlineUsers.Contains(currentSelectedPeer))
            {
                onlineUsersListBox.SelectedItem = currentSelectedPeer;
            }
            else if (_currentPeer == null && _onlineUsers.Count > 0)
            {
                onlineUsersListBox.SelectedIndex = 0;
                _currentPeer = "Broadcast";
                selectedPeerLabel.Text = "Đang chat với: Công cộng";
                selectedPeerLabel.Tag = "Broadcast";
            }
        }

        private void OnlineUsersListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (onlineUsersListBox.SelectedItem != null)
            {
                string selectedPeer = onlineUsersListBox.SelectedItem.ToString();
                if (_currentPeer != selectedPeer)
                {
                    _currentPeer = selectedPeer;
                    selectedPeerLabel.Text = $"Đang chat với: {selectedPeer}";
                    selectedPeerLabel.Tag = selectedPeer;

                    LoadChatHistory(selectedPeer);

                    _typingStatusLabel.Text = "";
                    _remoteTypingUser = "";
                }
            }
        }

        private async void sendButton_Click(object sender, EventArgs e)
        {
            string messageText = messageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(messageText) || messageTextBox.ForeColor == Color.Gray) return;

            string selectedPeer = selectedPeerLabel.Tag as string ?? "Broadcast";

            ChatMessage myMessage = new ChatMessage(_myUserName, messageText, true);
            AddMessageToChat(myMessage);

            SaveChatHistory(selectedPeer, myMessage);

            try
            {
                if (selectedPeer == "Broadcast")
                {
                    await _networkService.SendMulticastMessage(messageText);
                }
                else
                {
                    await _networkService.SendMessageToPeer(selectedPeer, messageText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể gửi tin nhắn: {ex.Message}", "Lỗi gửi tin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AddMessageToChat(new ChatMessage("Hệ thống", $"Lỗi gửi tin nhắn đến {selectedPeer}: {ex.Message}", false, ChatMessage.MessageType.Error));
            }

            messageTextBox.Clear();
            SetPlaceholder();
            _typingTimer.Stop();
            _isTyping = false;
            SendTypingStatus(false);
        }

        private void messageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Shift)
            {
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                sendButton_Click(sender, EventArgs.Empty);
            }
        }

        private void ChatListBox_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _chatMessages.Count) return;

            ChatMessage message = _chatMessages[e.Index];
            if (message.CalculatedTotalSize.IsEmpty)
            {
                using (Graphics g = chatListBox.CreateGraphics())
                {
                    MessageRenderer.PrepareMessageForDrawing(message, g, chatListBox.Width - AvatarWidth - AvatarPadding - 20, chatListBox.Font);
                }
            }
            e.ItemHeight = (int)message.CalculatedTotalSize.Height + 10; // Giữ nguyên khoảng cách thêm
        }

        private void ChatListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _chatMessages.Count) return;

            ChatMessage message = _chatMessages[e.Index];
            if (message.CalculatedTotalSize.IsEmpty)
            {
                using (Graphics g = e.Graphics)
                {
                    MessageRenderer.PrepareMessageForDrawing(message, g, chatListBox.Width - AvatarWidth - AvatarPadding - 20, chatListBox.Font);
                }
            }

            Image avatar = message.IsMyMessage ? _myAvatar : GetAvatarForUser(message.SenderName);
            MessageRenderer.DrawMessage(e.Graphics, e.Bounds, message, e.State, chatListBox.Font, avatar);
        }

        private void ChatListBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = chatListBox.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches && index < _chatMessages.Count)
                {
                    ChatMessage message = _chatMessages[index];
                    Rectangle itemBounds = chatListBox.GetItemRectangle(index);

                    int horizontalBubblePadding = 15;
                    int verticalBubblePadding = 10;
                    int bubbleMarginFromEdge = 10;

                    RectangleF bubbleRect;
                    if (message.IsMyMessage)
                    {
                        bubbleRect = new RectangleF(
                            itemBounds.Right - ((int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding)) - bubbleMarginFromEdge - AvatarWidth - AvatarPadding,
                            itemBounds.Y + 5,
                            (int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding),
                            (int)message.CalculatedContentSize.Height + (2 * verticalBubblePadding));
                    }
                    else
                    {
                        bubbleRect = new RectangleF(
                            itemBounds.X + bubbleMarginFromEdge + AvatarWidth + AvatarPadding,
                            itemBounds.Y + 5,
                            (int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding),
                            (int)message.CalculatedContentSize.Height + (2 * verticalBubblePadding));
                    }

                    RectangleF contentRect = new RectangleF(
                        bubbleRect.X + horizontalBubblePadding,
                        bubbleRect.Y + verticalBubblePadding,
                        message.CalculatedContentSize.Width,
                        message.CalculatedContentSize.Height);
                    PointF clickRelative = new PointF(e.X - contentRect.X, e.Y - contentRect.Y);

                    foreach (var urlRegion in message.UrlRegions)
                    {
                        if (urlRegion.Bounds.Contains(clickRelative))
                        {
                            try
                            {
                                string urlToOpen = urlRegion.Url;
                                if (!urlToOpen.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                                    !urlToOpen.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!urlToOpen.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && urlToOpen.Contains("."))
                                    {
                                        urlToOpen = "http://www." + urlToOpen;
                                    }
                                    else
                                    {
                                        urlToOpen = "http://" + urlToOpen;
                                    }
                                }
                                Process.Start(new ProcessStartInfo(urlToOpen) { UseShellExecute = true });
                                return;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Không thể mở liên kết: {ex.Message}", "Lỗi Mở Liên Kết", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                }
            }
        }

        private void ChatListBox_MouseMove(object sender, MouseEventArgs e)
        {
            bool isOverUrl = false;
            int index = chatListBox.IndexFromPoint(e.Location);

            if (index != ListBox.NoMatches && index < _chatMessages.Count)
            {
                ChatMessage message = _chatMessages[index];
                Rectangle itemBounds = chatListBox.GetItemRectangle(index);
                int horizontalBubblePadding = 15;
                int verticalBubblePadding = 10;
                int bubbleMarginFromEdge = 10;

                RectangleF bubbleRect;
                if (message.IsMyMessage)
                {
                    bubbleRect = new RectangleF(
                        itemBounds.Right - ((int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding)) - bubbleMarginFromEdge - AvatarWidth - AvatarPadding,
                        itemBounds.Y + 5,
                        (int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding),
                        (int)message.CalculatedContentSize.Height + (2 * verticalBubblePadding));
                }
                else
                {
                    bubbleRect = new RectangleF(
                        itemBounds.X + bubbleMarginFromEdge + AvatarWidth + AvatarPadding,
                        itemBounds.Y + 5,
                        (int)message.CalculatedContentSize.Width + (2 * horizontalBubblePadding),
                        (int)message.CalculatedContentSize.Height + (2 * verticalBubblePadding));
                }

                RectangleF contentRect = new RectangleF(
                    bubbleRect.X + horizontalBubblePadding,
                    bubbleRect.Y + verticalBubblePadding,
                    message.CalculatedContentSize.Width,
                    message.CalculatedContentSize.Height);
                PointF mouseRelative = new PointF(e.X - contentRect.X, e.Y - contentRect.Y);

                foreach (var urlRegion in message.UrlRegions)
                {
                    if (urlRegion.Bounds.Contains(mouseRelative))
                    {
                        isOverUrl = true;
                        break;
                    }
                }
            }

            if (isOverUrl)
            {
                chatListBox.Cursor = Cursors.Hand;
            }
            else
            {
                chatListBox.Cursor = Cursors.Default;
            }
        }
    }
}