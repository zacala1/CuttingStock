using System.Collections.Generic;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.Tests
{
    /// <summary>
    /// In-memory fake of <see cref="IDialogService"/>. Records every prompt
    /// and lets tests script the user's response queues. Defaults: confirms
    /// return <c>true</c>; file prompts return null (cancelled).
    /// </summary>
    public sealed class FakeDialogService : IDialogService
    {
        public List<(string Severity, string Title, string Message)> Messages { get; } = new();
        public Queue<bool>     ConfirmResponses  { get; } = new();
        public Queue<string?>  SavePathResponses { get; } = new();
        public Queue<string?>  OpenPathResponses { get; } = new();

        public void ShowInfo(string title, string message)    => Messages.Add(("info",    title, message));
        public void ShowWarning(string title, string message) => Messages.Add(("warning", title, message));
        public void ShowError(string title, string message)   => Messages.Add(("error",   title, message));

        public bool Confirm(string title, string message)
        {
            Messages.Add(("confirm", title, message));
            return ConfirmResponses.Count > 0 ? ConfirmResponses.Dequeue() : true;
        }

        public string? PromptSaveFile(string title, string filter, string defaultFileName)
        {
            Messages.Add(("save", title, defaultFileName));
            return SavePathResponses.Count > 0 ? SavePathResponses.Dequeue() : null;
        }

        public string? PromptOpenFile(string title, string filter)
        {
            Messages.Add(("open", title, filter));
            return OpenPathResponses.Count > 0 ? OpenPathResponses.Dequeue() : null;
        }
    }
}
