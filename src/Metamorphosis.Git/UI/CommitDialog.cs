using System;
using System.Windows.Forms;

namespace Metamorphosis.Git.UI
{
    internal class CommitDialog : Form
    {
        private TextBox _messageBox;
        private TextBox _authorBox;
        private Button _okButton;
        private Button _cancelButton;

        public string CommitMessage => _messageBox.Text.Trim();
        public string Author => _authorBox.Text.Trim();

        internal CommitDialog(string defaultAuthor)
        {
            Text = "Metamorphosis Git — Commit";
            Width = 480;
            Height = 260;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var msgLabel = new Label { Text = "Commit message:", Left = 12, Top = 12, Width = 440 };
            _messageBox = new TextBox
            {
                Left = 12, Top = 32, Width = 440, Height = 80,
                Multiline = true, ScrollBars = ScrollBars.Vertical
            };

            var authorLabel = new Label { Text = "Author:", Left = 12, Top = 124, Width = 440 };
            _authorBox = new TextBox { Left = 12, Top = 142, Width = 440, Text = defaultAuthor };

            _okButton = new Button
            {
                Text = "Commit", Left = 280, Top = 180, Width = 80, Height = 28,
                DialogResult = DialogResult.OK
            };
            _cancelButton = new Button
            {
                Text = "Cancel", Left = 370, Top = 180, Width = 80, Height = 28,
                DialogResult = DialogResult.Cancel
            };

            _okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_messageBox.Text))
                {
                    MessageBox.Show("Please enter a commit message.", "Metamorphosis Git",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (string.IsNullOrWhiteSpace(_authorBox.Text))
                {
                    MessageBox.Show("Please enter an author name.", "Metamorphosis Git",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                Close();
            };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.AddRange(new Control[]
            {
                msgLabel, _messageBox, authorLabel, _authorBox, _okButton, _cancelButton
            });
        }
    }
}
