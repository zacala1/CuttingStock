using System;

namespace CuttingStock.UI.Services
{
    public sealed record ExportDialogRequest(
        string Title,
        string Filter,
        string DefaultFileName,
        string SuccessMessage,
        string UnavailableMessage);

    /// <summary>Coordinates export guards, file selection, and user feedback.</summary>
    public static class ExportWorkflow
    {
        public static bool TryExport(
            IDialogService dialog,
            bool hasData,
            ExportDialogRequest request,
            Action<string> export)
        {
            ArgumentNullException.ThrowIfNull(dialog);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(export);

            if (!hasData)
            {
                dialog.ShowWarning("내보내기 불가", request.UnavailableMessage);
                return false;
            }

            string? path = dialog.PromptSaveFile(
                request.Title,
                request.Filter,
                request.DefaultFileName);
            if (path == null) return false;

            try
            {
                export(path);
                dialog.ShowInfo("저장 완료", $"{request.SuccessMessage}\n{path}");
                return true;
            }
            catch (Exception ex)
            {
                dialog.ShowError("오류", $"내보내기 오류: {ex.Message}");
                return false;
            }
        }
    }
}
