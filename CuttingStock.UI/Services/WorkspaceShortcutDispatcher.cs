using System;
using System.Windows.Input;

namespace CuttingStock.UI.Services
{
    public enum WorkspaceShortcut
    {
        LoadExample,
        Calculate,
        Compare,
        ExportToExcel,
        Cancel,
    }

    public sealed record WorkspaceShortcutTarget(
        ICommand LoadExampleCommand,
        ICommand CalculateCommand,
        ICommand CompareCommand,
        ICommand ExportToExcelCommand,
        ICommand CancelCommand,
        Func<bool> CanExport);

    public static class WorkspaceShortcutDispatcher
    {
        public static bool TryExecute(
            int selectedWorkspaceIndex,
            WorkspaceShortcut shortcut,
            WorkspaceShortcutTarget oneD,
            WorkspaceShortcutTarget twoD)
        {
            if (selectedWorkspaceIndex is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(selectedWorkspaceIndex));

            var target = selectedWorkspaceIndex == 1 ? twoD : oneD;
            if (shortcut == WorkspaceShortcut.ExportToExcel && !target.CanExport())
                return false;

            var command = shortcut switch
            {
                WorkspaceShortcut.LoadExample => target.LoadExampleCommand,
                WorkspaceShortcut.Calculate => target.CalculateCommand,
                WorkspaceShortcut.Compare => target.CompareCommand,
                WorkspaceShortcut.ExportToExcel => target.ExportToExcelCommand,
                WorkspaceShortcut.Cancel => target.CancelCommand,
                _ => throw new ArgumentOutOfRangeException(nameof(shortcut)),
            };

            if (!command.CanExecute(null))
                return false;

            command.Execute(null);
            return true;
        }
    }
}
