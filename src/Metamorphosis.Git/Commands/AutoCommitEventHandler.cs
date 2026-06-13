using System;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Metamorphosis.Repository;

namespace Metamorphosis.Git.Commands
{
    public class AutoCommitEventHandler : IExternalEventHandler
    {
        private Document _pendingDoc;
        private string _triggerReason;

        public void Arm(Document doc, string reason)
        {
            _pendingDoc = doc;
            _triggerReason = reason;
        }

        public void Execute(UIApplication app)
        {
            Document doc = _pendingDoc;
            _pendingDoc = null;
            if (doc == null) return;

            try
            {
                using var repo = Repository.Repository.OpenOrCreate(doc.PathName);

                if (!repo.GetAutoCommitEnabled()) return;

                string template = repo.GetAutoCommitTemplate();
                string msg = template
                    .Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
                    .Replace("{User}", Environment.UserName);
                if (!string.IsNullOrEmpty(_triggerReason))
                    msg += $" [{_triggerReason}]";

                string tempSnapshot = Path.Combine(Path.GetTempPath(),
                    Guid.NewGuid().ToString("N") + ".sdb");

                try
                {
                    var maker = new SnapshotMaker(doc, tempSnapshot);
                    maker.Export();
                    repo.Commit(tempSnapshot, msg, Environment.UserName);
                }
                finally
                {
                    if (File.Exists(tempSnapshot))
                        try { File.Delete(tempSnapshot); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AutoCommit failed: " + ex.Message);
            }
        }

        public string GetName() => "Metamorphosis Git AutoCommit";
    }
}
