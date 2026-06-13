using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Repository;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using WinForm = System.Windows.Forms.Form;
using WinControl = System.Windows.Forms.Control;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RollbackCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
                UIApplication uiApp = commandData.Application;

                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                IList<Metamorphosis.Objects.CommitInfo> log = repo.GetLog();

                if (log.Count == 0)
                {
                    TaskDialog.Show("Metamorphosis Git", "No commits to roll back to.");
                    return Result.Succeeded;
                }

                Metamorphosis.Objects.CommitInfo target = PickCommit(log);
                if (target == null) return Result.Cancelled;

                string snapshotPath = repo.GetSnapshotPath(target.Id);
                if (snapshotPath == null)
                {
                    message = $"Snapshot file for commit #{target.Id} not found.";
                    return Result.Failed;
                }

                var restore = LoadRestoreData(snapshotPath);

                int paramCount = restore.Values.Sum(d => d.Count);
                var confirm = TaskDialog.Show("Metamorphosis Git — Rollback",
                    $"Roll back to commit #{target.Id}: \"{target.Message}\"?"
                    + Environment.NewLine + Environment.NewLine
                    + $"  Elements to update: {restore.Count}"
                    + Environment.NewLine
                    + $"  Parameters to restore: {paramCount}"
                    + Environment.NewLine + Environment.NewLine
                    + "Geometry changes (moves/rotations) are NOT rolled back.",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel);

                if (confirm != TaskDialogResult.Yes) return Result.Cancelled;

                int restoredElems = 0;
                int restoredParams = 0;
                int skippedParams = 0;

                using (var tx = new Transaction(doc, $"Rollback to commit #{target.Id}"))
                {
                    tx.Start();
                    foreach (var kv in restore)
                    {
                        Element e = doc.GetElement(kv.Key);
                        if (e == null) continue;
                        bool anySet = false;
                        foreach (var pv in kv.Value)
                        {
                            Parameter p = e.LookupParameter(pv.Key);
                            if (p == null || p.IsReadOnly) { skippedParams++; continue; }
                            try
                            {
                                bool ok = SetParameterFromString(p, pv.Value);
                                if (ok) { restoredParams++; anySet = true; }
                                else skippedParams++;
                            }
                            catch { skippedParams++; }
                        }
                        if (anySet) restoredElems++;
                    }
                    tx.Commit();
                }

                TaskDialog.Show("Metamorphosis Git — Rollback Complete",
                    $"Restored {restoredParams} parameters across {restoredElems} elements."
                    + (skippedParams > 0
                        ? $" ({skippedParams} read-only or unsupported parameters skipped.)"
                        : ""));

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static Metamorphosis.Objects.CommitInfo PickCommit(IList<Metamorphosis.Objects.CommitInfo> commits)
        {
            using var dlg = new WinForm();
            dlg.Text = "Metamorphosis Git — Select Commit to Roll Back To";
            dlg.Width = 600;
            dlg.Height = 360;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.StartPosition = FormStartPosition.CenterScreen;

            var list = new ListBox { Left = 12, Top = 12, Width = 560, Height = 260, IntegralHeight = false };
            foreach (var c in commits)
                list.Items.Add($"#{c.Id}  [{c.Branch}]  {c.Timestamp.Split('T')[0]}  {c.Message}");

            var ok = new Button { Text = "Roll Back", Left = 400, Top = 288, Width = 80, Height = 28, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Left = 492, Top = 288, Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            dlg.Controls.AddRange(new WinControl[] { list, ok, cancel });

            if (dlg.ShowDialog() != DialogResult.OK || list.SelectedIndex < 0)
                return null;

            return commits[list.SelectedIndex];
        }

        // Loads UniqueId → (paramName → value) from a snapshot .sdb file.
        private static Dictionary<string, Dictionary<string, string>> LoadRestoreData(string sdbPath)
        {
            var paramDict = new Dictionary<int, string>();
            var valueDict = new Dictionary<int, string>();
            var eavData = new Dictionary<string, Dictionary<string, string>>();
            // id → uniqueId mapping from _objects_id
            var idToUid = new Dictionary<long, string>();

            string dbFilename = sdbPath;
            if (sdbPath.StartsWith(@"\\")) dbFilename = @"\\" + sdbPath;

            using (var conn = new SQLiteConnection("Data Source=" + dbFilename + ";Version=3;"))
            {
                conn.Open();

                var cmd = conn.CreateCommand();
                cmd.CommandText = "select id,name FROM _objects_attr";
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) paramDict[r.GetInt32(0)] = r.GetString(1);

                cmd = conn.CreateCommand();
                cmd.CommandText = "select id,value FROM _objects_val";
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) valueDict[r.GetInt32(0)] = r.GetString(1);

                cmd = conn.CreateCommand();
                cmd.CommandText = "select id,external_id FROM _objects_id WHERE isType=0";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        long id = r.GetInt64(0);
                        string uid = r.GetString(1);
                        if (!string.IsNullOrEmpty(uid)) idToUid[id] = uid;
                    }
                }

                cmd = conn.CreateCommand();
                cmd.CommandText = "select entity_id,attribute_id,value_id FROM _objects_eav";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        long entity_id = r.GetInt64(0);
                        int attr_id = r.GetInt32(1);
                        int val_id = r.GetInt32(2);

                        if (attr_id == -1002067) continue; // skip EDITED_BY
                        if (!idToUid.TryGetValue(entity_id, out string uid)) continue;
                        if (!paramDict.TryGetValue(attr_id, out string paramName)) continue;
                        if (!valueDict.TryGetValue(val_id, out string paramValue)) continue;

                        if (!eavData.TryGetValue(uid, out var dict))
                            eavData[uid] = dict = new Dictionary<string, string>();
                        dict[paramName] = paramValue;
                    }
                }
            }
            return eavData;
        }

        private static bool SetParameterFromString(Parameter p, string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            switch (p.StorageType)
            {
                case StorageType.String:
                    p.Set(value);
                    return true;
                case StorageType.Integer:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iv))
                    { p.Set(iv); return true; }
                    return p.SetValueString(value);
                case StorageType.Double:
                    return p.SetValueString(value);
                default:
                    return false;
            }
        }
    }
}
