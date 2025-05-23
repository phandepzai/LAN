using System;
using System.Drawing;
using System.Windows.Forms;

namespace Messenger
{
    public class CustomBalloonForm : Form
    {
        private Timer _timer;
        private Label _titleLabel;
        private Label _messageLabel;
        private PictureBox _iconPictureBox;
        private Action _onClick;

        public CustomBalloonForm(string title, string message, Icon icon, int timeout, Action onClick)
        {
            // Thiết lập form
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.White;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(300, 100);
            this.Opacity = 0.95;

            // Tạo PictureBox cho biểu tượng
            _iconPictureBox = new PictureBox
            {
                Size = new Size(32, 32),
                Location = new Point(10, 10),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            if (icon != null)
            {
                _iconPictureBox.Image = icon.ToBitmap();
            }
            this.Controls.Add(_iconPictureBox);

            // Tạo Label cho tiêu đề
            _titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(50, 10),
                AutoSize = true
            };
            this.Controls.Add(_titleLabel);

            // Tạo Label cho nội dung
            _messageLabel = new Label
            {
                Text = message,
                Font = new Font("Segoe UI", 9),
                Location = new Point(50, 30),
                Size = new Size(240, 60),
                AutoEllipsis = true
            };
            this.Controls.Add(_messageLabel);

            // Thiết lập timer để tự đóng
            _timer = new Timer
            {
                Interval = timeout
            };
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                this.Close();
            };

            // Xử lý nhấp chuột
            _onClick = onClick;
            this.Click += (s, e) => { _onClick?.Invoke(); this.Close(); };
            _titleLabel.Click += (s, e) => { _onClick?.Invoke(); this.Close(); };
            _messageLabel.Click += (s, e) => { _onClick?.Invoke(); this.Close(); };
            _iconPictureBox.Click += (s, e) => { _onClick?.Invoke(); this.Close(); };

            // Vẽ viền bo tròn
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, this.Width, this.Height, 10, 10));
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Rectangle workingArea = Screen.GetWorkingArea(this);
            this.Location = new Point(workingArea.Right - this.Width - 10, workingArea.Bottom - this.Height - 10);
            _timer.Tick += (s, ev) =>
            {
                for (double opacity = 0.95; opacity > 0; opacity -= 0.05)
                {
                    this.Opacity = opacity;
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(50);
                }
                _timer.Stop();
                this.Close();
            };
            _timer.Start();
        }

        // Hàm Win32 để tạo viền bo tròn
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                _iconPictureBox?.Dispose();
                _titleLabel?.Dispose();
                _messageLabel?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}