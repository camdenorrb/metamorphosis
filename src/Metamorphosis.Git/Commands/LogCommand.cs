using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Git.UI;
using Metamorphosis.Objects;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class LogCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                IList<CommitInfo> log = repo.GetLog();

                if (log.Count == 0)
                {
                    TaskDialog.Show("Metamorphosis Git",
                        "No commits yet for this model. Use Commit to save the current state.");
                    return Result.Succeeded;
                }

                using var form = new CommitLogForm(log, repo.CurrentBranch);
                form.ShowDialog();
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
