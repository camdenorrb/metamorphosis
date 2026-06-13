using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Metamorphosis.Objects;

namespace Metamorphosis.Git.UI
{
    internal class DiffResultsForm : Form
    {
        internal DiffResultsForm(IList<Change> changes, string title)
        {
            Text = title;
            Width = 900;
            Height = 520;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var grid = new DataGridView
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

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",        HeaderText = "Change Type",   FillWeight = 14 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category",    HeaderText = "Category",      FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ElementId",   HeaderText = "Element ID",    FillWeight = 10 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "Description",   FillWeight = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Level",       HeaderText = "Level",         FillWeight = 18 });

            foreach (var c in changes)
            {
                grid.Rows.Add(
                    c.ChangeType.ToString(),
                    c.Category ?? "",
                    c.ElementId,
                    c.ChangeDescription ?? "",
                    c.Level ?? "");
            }

            var summaryLabel = new Label
            {
                Text = $"{changes.Count} change(s) found.",
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 0)
            };

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

            Controls.Add(grid);
            Controls.Add(summaryLabel);
            Controls.Add(panel);
        }
    }
}
