namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Abstraction over WPF's MessageBox + file dialogs so a ViewModel can
    /// request user feedback without referencing System.Windows directly.
    /// The view implements this against the live WPF dialogs.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Information message — single OK button.</summary>
        void ShowInfo(string title, string message);

        /// <summary>Warning message — single OK button.</summary>
        void ShowWarning(string title, string message);

        /// <summary>Error message — single OK button.</summary>
        void ShowError(string title, string message);

        /// <summary>Yes/No confirmation. Returns true on Yes.</summary>
        bool Confirm(string title, string message);

        /// <summary>
        /// Show a SaveFileDialog. Returns the chosen path, or null if cancelled.
        /// </summary>
        string? PromptSaveFile(string title, string filter, string defaultFileName);

        /// <summary>
        /// Show an OpenFileDialog. Returns the chosen path, or null if cancelled.
        /// </summary>
        string? PromptOpenFile(string title, string filter);
    }
}
