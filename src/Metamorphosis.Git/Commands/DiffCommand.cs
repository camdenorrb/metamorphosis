using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Git.UI;
using Metamorphosis.Objects;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class DiffCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                IList<CommitInfo> log = repo.GetLog();

                if (log.Count < 2)
                {
                    TaskDialog.Show("Metamorphosis Git",
                        "At least two commits are required to diff.");
                    return Result.Succeeded;
                }

                using var picker = new CommitPickerForm(log);
                if (picker.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                CommitInfo from = picker.FromCommit;
                CommitInfo to = picker.ToCommit;

                string fromPath = repo.GetSnapshotPath(from.Id);
                string toPath = repo.GetSnapshotPath(to.Id);

                if (fromPath == null || toPath == null)
                {
                    TaskDialog.Show("Metamorphosis Git",
                        "One or both snapshot files could not be found.");
                    return Result.Failed;
                }

                var changes = ComparisonMaker.CompareOffline(fromPath, toPath);

                string title = $"Diff #{from.Id} → #{to.Id}";
                using var results = new DiffResultsForm(changes, "Metamorphosis Git — " + title);
                results.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
