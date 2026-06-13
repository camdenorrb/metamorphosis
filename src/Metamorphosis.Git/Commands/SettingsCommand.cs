using System;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Git.UI;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;
                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);
                using var form = new SettingsForm(repo);
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
