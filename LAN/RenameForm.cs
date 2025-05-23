using System;
using System.Windows.Forms;

namespace Messenger
{
    public partial class RenameForm : Form
    {
        public string NewUserName { get; private set; }

        public RenameForm(string currentUserName)
        {
            InitializeComponent();
            txtNewName.Text = currentUserName; // Set initial username
            txtNewName.SelectAll(); // Select all text for easy editing
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            // Debug: Log click event
            System.Diagnostics.Debug.WriteLine("BtnOK_Click triggered");

            string input = txtNewName.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                MessageBox.Show("Tên người dùng không được để trống.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewName.Focus();
                return;
            }

            NewUserName = input;
            DialogResult = DialogResult.OK;
            // Note: No need to call Close() explicitly; DialogResult should close the form
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            // Debug: Log click event
            System.Diagnostics.Debug.WriteLine("BtnCancel_Click triggered");

            DialogResult = DialogResult.Cancel;
            // Note: No need to call Close() explicitly; DialogResult should close the form
        }

        private void RenameForm_Load(object sender, EventArgs e)
        {

        }
    }
}