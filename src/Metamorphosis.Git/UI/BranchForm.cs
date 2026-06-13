using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Metamorphosis.Objects;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.UI
{
    internal class BranchForm : Form
    {
        private ListBox _branchList;
        private Button _newBtn;
        private Button _switchBtn;
        private Label _currentLabel;
        private readonly Repository _repo;

        internal BranchForm(Repository repo)
        {
            _repo = repo;
            Text = "Metamorphosis Git — Branches";
            Width = 420;
            Height = 360;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _currentLabel = new Label { Left = 12, Top = 8, Width = 380, Height = 20 };

            _branchList = new ListBox { Left = 12, Top = 32, Width = 380, Height = 220, IntegralHeight = false };

            _newBtn = new Button { Text = "New Branch", Left = 12, Top = 268, Width = 110, Height = 28 };
            _switchBtn = new Button { Text = "Switch To", Left = 134, Top = 268, Width = 90, Height = 28 };
            var closeBtn = new Button
            {
                Text = "Close", Left = 302, Top = 268, Width = 90, Height = 28,
                DialogResult = DialogResult.Cancel
            };

            _newBtn.Click += OnNew;
            _switchBtn.Click += OnSwitch;
            closeBtn.Click += (s, e) => Close();
            CancelButton = closeBtn;

            Controls.AddRange(new Control[]
            {
                _currentLabel, _branchList, _newBtn, _switchBtn, closeBtn
            });

            Refresh();
        }

        private void Refresh()
        {
            _branchList.Items.Clear();
            IList<BranchInfo> branches = _repo.GetBranches();
            foreach (var b in branches)
            {
                string marker = b.Name == _repo.CurrentBranch ? " ◀ current" : "";
                _branchList.Items.Add($"{b.Name}{marker}");
            }
            _currentLabel.Text = $"Current branch: {_repo.CurrentBranch}";
        }

        private void OnNew(object sender, EventArgs e)
        {
            string name = PromptInput("Enter name for new branch:", "New Branch");
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                _repo.CreateBranch(name);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Metamorphosis Git", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string PromptInput(string prompt, string title)
        {
            using var dlg = new Form();
            dlg.Text = title;
            dlg.Width = 360;
            dlg.Height = 140;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;
            dlg.StartPosition = FormStartPosition.CenterParent;

            var lbl = new Label { Text = prompt, Left = 12, Top = 12, Width = 320 };
            var tb = new TextBox { Left = 12, Top = 32, Width = 320 };
            var ok = new Button { Text = "OK", Left = 176, Top = 64, Width = 70, Height = 26, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 258, Top = 64, Width = 70, Height = 26, DialogResult = DialogResult.Cancel };
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            dlg.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });

            return dlg.ShowDialog() == DialogResult.OK ? tb.Text : string.Empty;
        }

        private void OnSwitch(object sender, EventArgs e)
        {
            if (_branchList.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a branch to switch to.", "Metamorphosis Git",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            IList<BranchInfo> branches = _repo.GetBranches();
            string selectedName = branches[_branchList.SelectedIndex].Name;
            try
            {
                _repo.CheckoutBranch(selectedName);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Metamorphosis Git", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
