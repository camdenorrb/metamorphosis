using System;
using System.IO;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Git.UI;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class CommitCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                using var dialog = new CommitDialog(Environment.UserName);
                if (dialog.ShowDialog() != DialogResult.OK)
                    return Result.Cancelled;

                string commitMessage = dialog.CommitMessage;
                string author = dialog.Author;

                string tempSnapshot = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + ".sdb");

                try
                {
                    var maker = new SnapshotMaker(doc, tempSnapshot);
                    maker.Export();

                    using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                    var commit = repo.Commit(tempSnapshot, commitMessage, author);

                    TaskDialog.Show("Metamorphosis Git",
                        $"Committed #{commit.Id}: {commitMessage}"
                        + Environment.NewLine
                        + $"Branch: {commit.Branch}  Hash: {commit.Hash.Substring(0, 8)}");
                }
                finally
                {
                    if (File.Exists(tempSnapshot))
                        try { File.Delete(tempSnapshot); } catch { }
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
