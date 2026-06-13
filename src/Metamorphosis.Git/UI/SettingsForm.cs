using System;
using System.Windows.Forms;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.UI
{
    internal class SettingsForm : Form
    {
        private CheckBox _autoCommitCheck;
        private TextBox _templateBox;
        private readonly Repository _repo;

        internal SettingsForm(Repository repo)
        {
            _repo = repo;
            Text = "Metamorphosis Git — Settings";
            Width = 480;
            Height = 220;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _autoCommitCheck = new CheckBox
            {
                Text = "Auto-commit on save / sync-to-central",
                Left = 12, Top = 16, Width = 440,
                Checked = repo.GetAutoCommitEnabled()
            };

            var templateLabel = new Label
            {
                Text = "Commit message template ({DateTime}, {User}):",
                Left = 12, Top = 52, Width = 440
            };
            _templateBox = new TextBox
            {
                Left = 12, Top = 70, Width = 440,
                Text = repo.GetAutoCommitTemplate()
            };

            var saveBtn = new Button
            {
                Text = "Save", Left = 290, Top = 140, Width = 80, Height = 28,
                DialogResult = DialogResult.OK
            };
            var cancelBtn = new Button
            {
                Text = "Cancel", Left = 380, Top = 140, Width = 80, Height = 28,
                DialogResult = DialogResult.Cancel
            };

            saveBtn.Click += (s, e) =>
            {
                repo.SetAutoCommitEnabled(_autoCommitCheck.Checked);
                repo.SetAutoCommitTemplate(_templateBox.Text);
                Close();
            };

            AcceptButton = saveBtn;
            CancelButton = cancelBtn;

            Controls.AddRange(new Control[]
            {
                _autoCommitCheck, templateLabel, _templateBox, saveBtn, cancelBtn
            });
        }
    }
}
