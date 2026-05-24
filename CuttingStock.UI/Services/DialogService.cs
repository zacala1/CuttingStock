using System.Windows;
using Microsoft.Win32;

namespace CuttingStock.UI.Services
{
    /// <summary>WPF implementation of <see cref="IDialogService"/>.</summary>
    public sealed class DialogService : IDialogService
    {
        public void ShowInfo(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public void ShowWarning(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void ShowError(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool Confirm(string title, string message) =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes;

        public string? PromptSaveFile(string title, string filter, string defaultFileName)
        {
            var dlg = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public string? PromptOpenFile(string title, string filter)
        {
            var dlg = new OpenFileDialog { Title = title, Filter = filter };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}
