using System;
using Autodesk.Revit.UI;

namespace Metamorphosis.Git
{
    public class ExternalApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                BuildUI(application);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                var td = new TaskDialog("Metamorphosis Git — Setup Error");
                td.ExpandedContent = ex.GetType().Name + ": " + ex.Message
                    + Environment.NewLine + ex.StackTrace;
                td.Show();
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
            => Result.Succeeded;

        private static void BuildUI(UIControlledApplication app)
        {
            string asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var panel = app.CreateRibbonPanel(Tab.AddIns, "Metamorphosis" + Environment.NewLine + "Git");

            var commit = new PushButtonData(
                "GitCommit", "Commit", asmPath,
                "Metamorphosis.Git.Commands.CommitCommand");
            commit.ToolTip = "Save a named commit of the current model state";

            var log = new PushButtonData(
                "GitLog", "Log", asmPath,
                "Metamorphosis.Git.Commands.LogCommand");
            log.ToolTip = "View the commit history for this model";

            var status = new PushButtonData(
                "GitStatus", "Status", asmPath,
                "Metamorphosis.Git.Commands.StatusCommand");
            status.ToolTip = "Compare the current model against the latest commit";

            var diff = new PushButtonData(
                "GitDiff", "Diff", asmPath,
                "Metamorphosis.Git.Commands.DiffCommand");
            diff.ToolTip = "Compare any two commits";

            var branch = new PushButtonData(
                "GitBranch", "Branch", asmPath,
                "Metamorphosis.Git.Commands.BranchCommand");
            branch.ToolTip = "Create or switch branches";

            panel.AddItem(commit);
            panel.AddItem(log);
            panel.AddItem(status);
            panel.AddItem(diff);
            panel.AddItem(branch);

            panel.AddSlideOut();

            var settings = new PushButtonData(
                "GitSettings", "Settings", asmPath,
                "Metamorphosis.Git.Commands.SettingsCommand");
            settings.ToolTip = "Configure auto-commit and other options";
            panel.AddItem(settings);
        }
    }
}
