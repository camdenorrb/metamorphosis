using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Metamorphosis.Objects;

namespace Metamorphosis.Git.UI
{
    internal class CommitPickerForm : Form
    {
        private ListBox _fromList;
        private ListBox _toList;
        private readonly IList<CommitInfo> _commits;

        public CommitInfo FromCommit { get; private set; }
        public CommitInfo ToCommit { get; private set; }

        internal CommitPickerForm(IList<CommitInfo> commits)
        {
            _commits = commits;
            Text = "Metamorphosis Git — Select Commits to Diff";
            Width = 740;
            Height = 420;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var fromLabel = new Label { Text = "From (older):", Left = 12, Top = 8, Width = 340 };
            _fromList = new ListBox { Left = 12, Top = 26, Width = 340, Height = 300, IntegralHeight = false };

            var toLabel = new Label { Text = "To (newer):", Left = 370, Top = 8, Width = 340 };
            _toList = new ListBox { Left = 370, Top = 26, Width = 340, Height = 300, IntegralHeight = false };

            foreach (var c in commits)
            {
                string entry = $"#{c.Id}  [{c.Branch}]  {c.Timestamp.Split('T')[0]}  {c.Message}";
                _fromList.Items.Add(entry);
                _toList.Items.Add(entry);
            }

            var okBtn = new Button
            {
                Text = "Diff", Left = 540, Top = 340, Width = 80, Height = 28,
                DialogResult = DialogResult.OK
            };
            var cancelBtn = new Button
            {
                Text = "Cancel", Left = 630, Top = 340, Width = 80, Height = 28,
                DialogResult = DialogResult.Cancel
            };

            okBtn.Click += (s, e) =>
            {
                if (_fromList.SelectedIndex < 0 || _toList.SelectedIndex < 0)
                {
                    MessageBox.Show("Please select both a From and a To commit.", "Metamorphosis Git",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (_fromList.SelectedIndex == _toList.SelectedIndex)
                {
                    MessageBox.Show("Please select two different commits.", "Metamorphosis Git",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                FromCommit = _commits[_fromList.SelectedIndex];
                ToCommit = _commits[_toList.SelectedIndex];
                Close();
            };

            AcceptButton = okBtn;
            CancelButton = cancelBtn;

            Controls.AddRange(new Control[]
            {
                fromLabel, _fromList, toLabel, _toList, okBtn, cancelBtn
            });
        }
    }
}
