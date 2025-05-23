﻿namespace Messenger
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Mã được tạo bởi trình thiết kế Windows Form

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.chatListBox = new System.Windows.Forms.ListBox();
            this.messageTextBox = new System.Windows.Forms.RichTextBox();
            this.onlineUsersListBox = new System.Windows.Forms.ListBox();
            this.selectedPeerLabel = new System.Windows.Forms.Label();
            this._typingStatusLabel = new System.Windows.Forms.Label();
            this.messageContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.btnRename = new System.Windows.Forms.Button();
            this.timeLabel = new System.Windows.Forms.Label();
            this.dateLabel = new System.Windows.Forms.Label();
            this.authorLabel = new System.Windows.Forms.Label();
            this.dayLabel = new System.Windows.Forms.Label();
            this.messageContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // chatListBox
            // 
            this.chatListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chatListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.chatListBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.chatListBox.FormattingEnabled = true;
            this.chatListBox.ItemHeight = 16;
            this.chatListBox.Location = new System.Drawing.Point(171, 26);
            this.chatListBox.Margin = new System.Windows.Forms.Padding(2);
            this.chatListBox.Name = "chatListBox";
            this.chatListBox.Size = new System.Drawing.Size(533, 576);
            this.chatListBox.TabIndex = 0;
            // 
            // messageTextBox
            // 
            this.messageTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.messageTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.messageTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.messageTextBox.Location = new System.Drawing.Point(171, 622);
            this.messageTextBox.Margin = new System.Windows.Forms.Padding(2);
            this.messageTextBox.Name = "messageTextBox";
            this.messageTextBox.Size = new System.Drawing.Size(529, 68);
            this.messageTextBox.TabIndex = 1;
            this.messageTextBox.Text = "";
            this.messageTextBox.TextChanged += new System.EventHandler(this.messageTextBox_TextChanged);
            // 
            // onlineUsersListBox
            // 
            this.onlineUsersListBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.onlineUsersListBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.onlineUsersListBox.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.onlineUsersListBox.FormattingEnabled = true;
            this.onlineUsersListBox.ItemHeight = 17;
            this.onlineUsersListBox.Location = new System.Drawing.Point(9, 50);
            this.onlineUsersListBox.Margin = new System.Windows.Forms.Padding(2);
            this.onlineUsersListBox.Name = "onlineUsersListBox";
            this.onlineUsersListBox.Size = new System.Drawing.Size(149, 323);
            this.onlineUsersListBox.TabIndex = 3;
            // 
            // selectedPeerLabel
            // 
            this.selectedPeerLabel.AutoSize = true;
            this.selectedPeerLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.selectedPeerLabel.Location = new System.Drawing.Point(168, 3);
            this.selectedPeerLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.selectedPeerLabel.Name = "selectedPeerLabel";
            this.selectedPeerLabel.Size = new System.Drawing.Size(109, 19);
            this.selectedPeerLabel.TabIndex = 4;
            this.selectedPeerLabel.Text = "Đang chat với: ";
            // 
            // _typingStatusLabel
            // 
            this._typingStatusLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._typingStatusLabel.AutoSize = true;
            this._typingStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.75F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._typingStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this._typingStatusLabel.Location = new System.Drawing.Point(174, 585);
            this._typingStatusLabel.Name = "_typingStatusLabel";
            this._typingStatusLabel.Size = new System.Drawing.Size(0, 15);
            this._typingStatusLabel.TabIndex = 6;
            // 
            // messageContextMenuStrip
            // 
            this.messageContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem});
            this.messageContextMenuStrip.Name = "messageContextMenuStrip";
            this.messageContextMenuStrip.Size = new System.Drawing.Size(123, 26);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(122, 22);
            this.copyToolStripMenuItem.Text = "Sao chép";
            // 
            // btnRename
            // 
            this.btnRename.Location = new System.Drawing.Point(35, 12);
            this.btnRename.Name = "btnRename";
            this.btnRename.Size = new System.Drawing.Size(75, 33);
            this.btnRename.TabIndex = 7;
            this.btnRename.Text = "Đổi tên";
            this.btnRename.UseVisualStyleBackColor = true;
            this.btnRename.Click += new System.EventHandler(this.btnRename_Click);
            // 
            // timeLabel
            // 
            this.timeLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.timeLabel.AutoSize = true;
            this.timeLabel.Font = new System.Drawing.Font("Segoe UI", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.timeLabel.ForeColor = System.Drawing.Color.Green;
            this.timeLabel.Location = new System.Drawing.Point(29, 554);
            this.timeLabel.Name = "timeLabel";
            this.timeLabel.Size = new System.Drawing.Size(88, 25);
            this.timeLabel.TabIndex = 8;
            this.timeLabel.Text = "00:00:00";
            // 
            // dateLabel
            // 
            this.dateLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.dateLabel.AutoSize = true;
            this.dateLabel.Font = new System.Drawing.Font("Consolas", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dateLabel.ForeColor = System.Drawing.SystemColors.Highlight;
            this.dateLabel.Location = new System.Drawing.Point(29, 585);
            this.dateLabel.Name = "dateLabel";
            this.dateLabel.Size = new System.Drawing.Size(88, 18);
            this.dateLabel.TabIndex = 9;
            this.dateLabel.Text = "01/01/2025";
            // 
            // authorLabel
            // 
            this.authorLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.authorLabel.AutoSize = true;
            this.authorLabel.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.authorLabel.ForeColor = System.Drawing.Color.Silver;
            this.authorLabel.Location = new System.Drawing.Point(20, 670);
            this.authorLabel.Name = "authorLabel";
            this.authorLabel.Size = new System.Drawing.Size(99, 26);
            this.authorLabel.TabIndex = 10;
            this.authorLabel.Text = "@nongvanphan\nFAB Inspection Part";
            // 
            // dayLabel
            // 
            this.dayLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.dayLabel.AutoSize = true;
            this.dayLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dayLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.dayLabel.Location = new System.Drawing.Point(39, 610);
            this.dayLabel.Name = "dayLabel";
            this.dayLabel.Size = new System.Drawing.Size(56, 19);
            this.dayLabel.TabIndex = 11;
            this.dayLabel.Text = "Thứ Hai";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(711, 701);
            this.Controls.Add(this.dayLabel);
            this.Controls.Add(this.authorLabel);
            this.Controls.Add(this.dateLabel);
            this.Controls.Add(this.timeLabel);
            this.Controls.Add(this.btnRename);
            this.Controls.Add(this._typingStatusLabel);
            this.Controls.Add(this.selectedPeerLabel);
            this.Controls.Add(this.onlineUsersListBox);
            this.Controls.Add(this.messageTextBox);
            this.Controls.Add(this.chatListBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimumSize = new System.Drawing.Size(539, 404);
            this.Name = "MainForm";
            this.Text = "Ứng dụng Chat LAN";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.messageContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.RichTextBox messageTextBox;
        private System.Windows.Forms.ListBox chatListBox;
        private System.Windows.Forms.ListBox onlineUsersListBox;
        private System.Windows.Forms.Label selectedPeerLabel;
        private System.Windows.Forms.ContextMenuStrip messageContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.Label _typingStatusLabel;
        private System.Windows.Forms.Button btnRename;
        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.Label dateLabel;
        private System.Windows.Forms.Label authorLabel;
        private System.Windows.Forms.Label dayLabel;
    }
}