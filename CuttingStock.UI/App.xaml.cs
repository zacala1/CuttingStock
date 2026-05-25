using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CuttingStock
{
    /// <summary>
    /// Interaction logic for App.xaml. Adds last-resort exception handlers so
    /// async / event-handler crashes are surfaced to the user and logged rather
    /// than silently terminating the process.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // UI-thread exceptions raised through dispatched callbacks (event
            // handlers, async-void). Mark Handled so the app stays alive.
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Non-UI thread exceptions (Task.Run continuations not awaited, etc.).
            // .NET still tears down on these, but we get a chance to log first.
            AppDomain.CurrentDomain.UnhandledException += App_UnhandledException;

            // Unobserved Task exceptions — Task that throws + nothing awaits it.
            TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("UI thread", e.Exception);
            MessageBox.Show(
                $"예기치 못한 오류가 발생했습니다. 작업 내용을 저장한 뒤 앱을 재시작해주세요.\n\n{e.Exception.Message}",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;   // keep the app alive
        }

        private static void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("AppDomain", e.ExceptionObject as Exception);
            // Cannot mark handled here — process termination is imminent.
        }

        private static void App_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("Task", e.Exception);
            e.SetObserved();    // prevent process tear-down on .NET 4.5+ default
        }

        /// <summary>
        /// Best-effort exception logger. Writes to
        /// <c>%LOCALAPPDATA%/CuttingStock/crash.log</c> with the timestamp and
        /// the exception's ToString() (which includes the stack trace). Swallows
        /// any I/O failure — we must never throw from a crash handler.
        /// </summary>
        private static void LogException(string source, Exception? ex)
        {
            if (ex == null) return;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CuttingStock");
                Directory.CreateDirectory(dir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}\n{ex}\n\n";
                File.AppendAllText(Path.Combine(dir, "crash.log"), line);
            }
            catch { /* never throw from a crash handler */ }
        }
    }
}
