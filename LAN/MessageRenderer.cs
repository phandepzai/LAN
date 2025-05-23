using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Collections.Generic;

namespace Messenger
{
    public static class MessageRenderer
    {
        private static readonly Regex UrlRegex = new Regex(@"\b(?:https?://|www\.)?([a-zA-Z0-9-]+\.)+[a-zA-Z]{2,6}(?:/\S*)?\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly SolidBrush MyMessageBackgroundBrush = new SolidBrush(Color.FromArgb(152, 251, 152));
        private static readonly SolidBrush OtherMessageBackgroundBrush = new SolidBrush(Color.PaleGoldenrod);
        private static readonly SolidBrush MyMessageTextBrush = new SolidBrush(Color.Black);
        private static readonly SolidBrush OtherMessageTextBrush = new SolidBrush(Color.Black);
        private static readonly SolidBrush TimestampBrush = new SolidBrush(Color.Gray);
        private static readonly SolidBrush UrlBrush = new SolidBrush(Color.Blue);
        private static Font _timestampFont;

        public static void Initialize(Font baseFont)
        {
            if (baseFont != null && _timestampFont == null)
            {
                _timestampFont = new Font(baseFont.FontFamily, baseFont.Size * 0.7f);
            }
        }

        private static string GetFormattedMessageText(ChatMessage message)
        {
            return message.Content; // Chỉ trả về nội dung tin nhắn
        }

        public static void PrepareMessageForDrawing(ChatMessage message, Graphics g, int listBoxWidth, Font defaultFont)
        {
            if (_timestampFont == null)
            {
                Initialize(new Font("Arial", 10));
            }

            message.UrlRegions.Clear();

            string displayText = GetFormattedMessageText(message);
            string senderName = message.IsMyMessage ? "" : message.SenderName;

            int maxBubbleContentWidth = (int)(listBoxWidth * 0.70f);
            int horizontalBubblePadding = 15;
            int verticalBubblePadding = 10;
            int timestampBubbleGap = 8;
            int itemBottomMargin = 8;
            int avatarSize = 40;
            int avatarPadding = 5;
            int senderNameGap = 5;

            // Tạo StringFormat hỗ trợ xuống dòng
            StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.MeasureTrailingSpaces,
                Trimming = StringTrimming.Word
            };

            // Đo kích thước nội dung tin nhắn
            float maxWidth = maxBubbleContentWidth - (2 * horizontalBubblePadding);
            float maxContentWidth = 0;

            // Chia nội dung tin nhắn thành các phần (trước URL, URL, sau URL) để đo kích thước
            MatchCollection matches = UrlRegex.Matches(displayText);
            int lastIndex = 0;
            float currentY = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    string preUrlText = displayText.Substring(lastIndex, match.Index - lastIndex);
                    SizeF preUrlSize = g.MeasureString(preUrlText, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);
                    maxContentWidth = Math.Max(maxContentWidth, preUrlSize.Width);
                    currentY += preUrlSize.Height; // Tăng chiều cao
                }

                string url = match.Value;
                SizeF urlSize = g.MeasureString(url, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);
                maxContentWidth = Math.Max(maxContentWidth, urlSize.Width);
                message.UrlRegions.Add(new UrlRegion(new RectangleF(0, currentY, urlSize.Width, urlSize.Height), url));
                currentY += urlSize.Height; // Tăng chiều cao

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < displayText.Length)
            {
                string remainingText = displayText.Substring(lastIndex);
                SizeF remainingSize = g.MeasureString(remainingText, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);
                maxContentWidth = Math.Max(maxContentWidth, remainingSize.Width);
                currentY += remainingSize.Height;
            }

            message.CalculatedContentSize = new SizeF(maxContentWidth, currentY);

            // Đo kích thước tên người gửi (nếu có)
            SizeF senderNameSizeF = new SizeF(0, 0);
            if (!string.IsNullOrEmpty(senderName))
            {
                senderNameSizeF = g.MeasureString(senderName, defaultFont, maxBubbleContentWidth, stringFormat);
                message.CalculatedSenderNameSize = new SizeF((float)Math.Ceiling(senderNameSizeF.Width), (float)Math.Ceiling(senderNameSizeF.Height));
            }
            else
            {
                message.CalculatedSenderNameSize = new SizeF(0, 0);
            }

            // Đo kích thước thời gian
            SizeF timestampSizeF = g.MeasureString(message.Timestamp.ToString("HH:mm"), _timestampFont, maxBubbleContentWidth, stringFormat);
            message.CalculatedTimestampSize = new SizeF((float)Math.Ceiling(timestampSizeF.Width), (float)Math.Ceiling(timestampSizeF.Height));

            float bubbleWidth = message.CalculatedContentSize.Width + (2 * horizontalBubblePadding);
            float bubbleHeight = message.CalculatedContentSize.Height + (2 * verticalBubblePadding);

            // Tính toán tổng kích thước
            message.CalculatedTotalSize = new SizeF(
                Math.Max(bubbleWidth, message.CalculatedSenderNameSize.Width) + (message.Avatar != null ? avatarSize + avatarPadding : 0) + 20,
                bubbleHeight + timestampBubbleGap + message.CalculatedTimestampSize.Height + itemBottomMargin + (senderNameSizeF.Height > 0 ? senderNameSizeF.Height + senderNameGap : 0)
            );
        }

        public static void DrawMessage(Graphics g, Rectangle bounds, ChatMessage message, DrawItemState state, Font defaultFont, Image avatar)
        {
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_timestampFont == null)
            {
                Initialize(defaultFont);
            }

            SolidBrush backgroundBrush = message.IsMyMessage ? MyMessageBackgroundBrush : OtherMessageBackgroundBrush;
            SolidBrush textBrush = message.IsMyMessage ? MyMessageTextBrush : OtherMessageTextBrush;
            string displayText = GetFormattedMessageText(message); // Chỉ lấy nội dung tin nhắn
            string senderName = message.IsMyMessage ? "" : message.SenderName; // Tên người gửi chỉ cho tin nhắn đến

            SizeF contentSize = message.CalculatedContentSize;
            SizeF timestampSize = message.CalculatedTimestampSize;
            SizeF senderNameSize = message.CalculatedSenderNameSize;

            int horizontalBubblePadding = 15;
            int verticalBubblePadding = 10;
            int borderRadius = 10;
            int bubbleMarginFromEdge = 10;
            int avatarSize = 40;
            int avatarPadding = 5;
            int senderNameGap = 5;

            int bubbleWidth = (int)contentSize.Width + (2 * horizontalBubblePadding);
            int bubbleHeight = (int)contentSize.Height + (2 * verticalBubblePadding);

            RectangleF bubbleRect;
            RectangleF avatarRect;
            RectangleF contentRect;
            RectangleF timestampRect;
            RectangleF senderNameRect = RectangleF.Empty;

            // Tính toán vị trí bong bóng, avatar, nội dung và tên người gửi
            if (!message.BubbleBounds.IsEmpty && !message.AvatarBounds.IsEmpty)
            {
                bubbleRect = message.BubbleBounds;
                avatarRect = message.AvatarBounds;
                contentRect = new RectangleF(bubbleRect.X + horizontalBubblePadding, bubbleRect.Y + verticalBubblePadding, contentSize.Width, contentSize.Height);
                timestampRect = new RectangleF(
                    message.IsMyMessage ? bubbleRect.Right - timestampSize.Width : bubbleRect.X,
                    bubbleRect.Bottom + 2,
                    timestampSize.Width,
                    timestampSize.Height);
                if (!string.IsNullOrEmpty(senderName))
                {
                    senderNameRect = new RectangleF(
                        bubbleRect.X,
                        bubbleRect.Y - senderNameSize.Height - senderNameGap,
                        senderNameSize.Width,
                        senderNameSize.Height);
                }
            }
            else
            {
                if (message.IsMyMessage)
                {
                    // Tin nhắn của tôi: bong bóng bên phải, avatar bên phải
                    bubbleRect = new RectangleF(
                        bounds.Right - bubbleWidth - bubbleMarginFromEdge - avatarSize - avatarPadding,
                        bounds.Y + 5,
                        bubbleWidth,
                        bubbleHeight);
                    avatarRect = new RectangleF(
                        bubbleRect.Right + avatarPadding,
                        bounds.Y + 5,
                        avatarSize,
                        avatarSize);
                    contentRect = new RectangleF(
                        bubbleRect.X + horizontalBubblePadding,
                        bubbleRect.Y + verticalBubblePadding,
                        contentSize.Width,
                        contentSize.Height);
                    timestampRect = new RectangleF(
                        bubbleRect.Right - timestampSize.Width,
                        bubbleRect.Bottom + 2,
                        timestampSize.Width,
                        timestampSize.Height);
                }
                else
                {
                    // Tin nhắn của người khác: avatar bên trái, bong bóng bên phải
                    avatarRect = new RectangleF(
                        bounds.X + bubbleMarginFromEdge,
                        bounds.Y + 5,
                        avatarSize,
                        avatarSize);
                    bubbleRect = new RectangleF(
                        avatarRect.Right + avatarPadding,
                        bounds.Y + 5 + (senderNameSize.Height > 0 ? senderNameSize.Height + senderNameGap : 0),
                        bubbleWidth,
                        bubbleHeight);
                    contentRect = new RectangleF(
                        bubbleRect.X + horizontalBubblePadding,
                        bubbleRect.Y + verticalBubblePadding,
                        contentSize.Width,
                        contentSize.Height);
                    timestampRect = new RectangleF(
                        bubbleRect.X,
                        bubbleRect.Bottom + 2,
                        timestampSize.Width,
                        timestampSize.Height);
                    if (!string.IsNullOrEmpty(senderName))
                    {
                        senderNameRect = new RectangleF(
                            bubbleRect.X,
                            bubbleRect.Y - senderNameSize.Height - senderNameGap,
                            senderNameSize.Width,
                            senderNameSize.Height);
                    }
                }
            }

            // Vẽ tên người gửi (nếu có)
            if (!string.IsNullOrEmpty(senderName))
            {
                g.DrawString(senderName, defaultFont, textBrush, senderNameRect, StringFormat.GenericTypographic);
            }

            // Vẽ avatar
            if (avatar != null)
            {
                g.DrawImage(avatar, avatarRect);
            }

            // Vẽ bong bóng
            using (GraphicsPath path = RoundedRectangle(Rectangle.Round(bubbleRect), borderRadius))
            {
                g.FillPath(backgroundBrush, path);
            }

            // Vẽ nội dung tin nhắn
            DrawFormattedText(g, displayText, contentRect, textBrush, defaultFont, message.UrlRegions);

            // Vẽ thời gian
            if (_timestampFont != null)
            {
                g.DrawString(message.Timestamp.ToString("HH:mm"), _timestampFont, TimestampBrush, timestampRect, StringFormat.GenericTypographic);
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private static void DrawFormattedText(Graphics g, string text, RectangleF rect, Brush defaultBrush, Font defaultFont, List<UrlRegion> urlRegions)
        {
            // Tạo StringFormat hỗ trợ xuống dòng tự động
            StringFormat stringFormat = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.MeasureTrailingSpaces,
                Trimming = StringTrimming.Word // Cắt từ để xuống dòng
            };

            MatchCollection matches = UrlRegex.Matches(text);
            int lastIndex = 0;
            float currentX = rect.X;
            float currentY = rect.Y;
            float maxWidth = rect.Width; // Chiều rộng tối đa của vùng vẽ
            int urlRegionIndex = 0;

            // Duyệt qua từng URL trong văn bản
            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    // Vẽ văn bản trước URL
                    string preUrlText = text.Substring(lastIndex, match.Index - lastIndex);
                    SizeF preUrlSize = g.MeasureString(preUrlText, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);

                    RectangleF preUrlRect = new RectangleF(currentX, currentY, maxWidth, preUrlSize.Height);
                    g.DrawString(preUrlText, defaultFont, defaultBrush, preUrlRect, stringFormat);

                    // Sau khi vẽ xong, xuống dòng
                    currentX = rect.X; // Đặt lại currentX để bắt đầu dòng mới
                    currentY += preUrlSize.Height; // Tăng currentY để xuống dòng
                }

                // Vẽ URL
                if (urlRegionIndex < urlRegions.Count)
                {
                    UrlRegion storedUrlRegion = urlRegions[urlRegionIndex];
                    string url = storedUrlRegion.Url;
                    SizeF urlSize = g.MeasureString(url, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);

                    // Điều chỉnh vị trí của URL region để đồng bộ với vị trí vẽ
                    storedUrlRegion.Bounds = new RectangleF(currentX - rect.X, currentY - rect.Y, urlSize.Width, urlSize.Height);
                    RectangleF urlRect = new RectangleF(currentX, currentY, maxWidth, urlSize.Height);
                    g.DrawString(url, defaultFont, UrlBrush, urlRect, stringFormat);

                    // Sau khi vẽ xong, xuống dòng
                    currentX = rect.X; // Đặt lại currentX để bắt đầu dòng mới
                    currentY += urlSize.Height; // Tăng currentY để xuống dòng

                    urlRegionIndex++;
                }

                lastIndex = match.Index + match.Length;
            }

            // Vẽ phần văn bản còn lại sau URL cuối cùng
            if (lastIndex < text.Length)
            {
                string remainingText = text.Substring(lastIndex);
                SizeF remainingSize = g.MeasureString(remainingText, defaultFont, new SizeF(maxWidth, float.MaxValue), stringFormat);

                RectangleF remainingRect = new RectangleF(currentX, currentY, maxWidth, remainingSize.Height);
                g.DrawString(remainingText, defaultFont, defaultBrush, remainingRect, stringFormat);
            }
        }
    }
}