using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Metamorphosis.Objects;

namespace Metamorphosis.Git.UI
{
    internal class CommitLogForm : Form
    {
        private DataGridView _grid;

        internal CommitLogForm(IList<CommitInfo> commits, string currentBranch)
        {
            Text = $"Metamorphosis Git — Commit Log  [{currentBranch}]";
            Width = 820;
            Height = 480;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false
            };

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id",        HeaderText = "#",        FillWeight = 4 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Branch",     HeaderText = "Branch",   FillWeight = 10 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Message",    HeaderText = "Message",  FillWeight = 35 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Author",     HeaderText = "Author",   FillWeight = 15 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp",  HeaderText = "When",     FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hash",       HeaderText = "Hash",     FillWeight = 12 });

            foreach (var c in commits)
            {
                _grid.Rows.Add(
                    c.Id,
                    c.Branch,
                    c.Message,
                    c.Author,
                    c.Timestamp,
                    c.Hash.Length >= 8 ? c.Hash.Substring(0, 8) : c.Hash);
            }

            var closeBtn = new Button
            {
                Text = "Close", Width = 80, Height = 28,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            closeBtn.Click += (s, e) => Close();

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
            closeBtn.Left = panel.Width - 92;
            closeBtn.Top = 6;
            panel.Resize += (s, e) => { closeBtn.Left = panel.Width - 92; };
            panel.Controls.Add(closeBtn);

            Controls.Add(_grid);
            Controls.Add(panel);
        }
    }
}
