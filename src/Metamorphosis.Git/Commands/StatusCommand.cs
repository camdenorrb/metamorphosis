using System;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Git.UI;
using Metamorphosis.Objects;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class StatusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                CommitInfo head = repo.GetHead();

                if (head == null)
                {
                    TaskDialog.Show("Metamorphosis Git — Status",
                        "No commits yet. Use Commit to save the current model state.");
                    return Result.Succeeded;
                }

                string headSnapshotPath = repo.GetSnapshotPath(head.Id);
                if (headSnapshotPath == null)
                {
                    TaskDialog.Show("Metamorphosis Git — Status",
                        $"Snapshot file for commit #{head.Id} not found.");
                    return Result.Failed;
                }

                string tempCurrent = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + ".sdb");

                try
                {
                    var maker = new SnapshotMaker(doc, tempCurrent);
                    maker.Export();

                    var changes = ComparisonMaker.CompareOffline(headSnapshotPath, tempCurrent);

                    string formTitle = $"Status vs commit #{head.Id}: {head.Message}";
                    using var form = new DiffResultsForm(changes, "Metamorphosis Git — " + formTitle);
                    form.ShowDialog();
                }
                finally
                {
                    if (File.Exists(tempCurrent))
                        try { File.Delete(tempCurrent); } catch { }
                }

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
