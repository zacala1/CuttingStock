namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Shared soft-cancel lifecycle for UI solver runs. Solvers may keep running
    /// in the background, so stale callbacks/results are gated by a run id.
    /// </summary>
    public sealed class SolverRunLifecycle : IDisposable
    {
        private int _currentRunId;
        private CancellationTokenSource? _currentCts;

        public int CurrentRunId => _currentRunId;

        public SolverRunScope Begin()
        {
            _currentRunId++;
            CancelAndDisposeCurrentSource();
            _currentCts = new CancellationTokenSource();
            return new SolverRunScope(_currentRunId, _currentCts.Token);
        }

        public void CancelCurrent()
        {
            _currentRunId++;
            CancelAndDisposeCurrentSource();
        }

        public bool IsCurrent(SolverRunScope scope) => scope.RunId == _currentRunId;

        public bool Complete(SolverRunScope scope)
        {
            if (!IsCurrent(scope)) return false;
            _currentRunId++;
            _currentCts?.Dispose();
            _currentCts = null;
            return true;
        }

        public async Task RunAsync(
            Action onStarted,
            Func<SolverRunScope, Task> executeAsync,
            Action? onCompleted = null,
            Action<Exception>? onError = null)
        {
            ArgumentNullException.ThrowIfNull(onStarted);
            ArgumentNullException.ThrowIfNull(executeAsync);

            var scope = Begin();

            try
            {
                onStarted();
                await executeAsync(scope);
            }
            catch (Exception ex) when (IsCurrent(scope))
            {
                if (onError == null) throw;
                onError(ex);
            }
            catch (Exception) when (!IsCurrent(scope))
            {
                // Stale run failed after cancellation/supersession; ignore the result.
            }
            finally
            {
                if (Complete(scope))
                    onCompleted?.Invoke();
            }
        }

        public Progress<double> CreateProgress(SolverRunScope scope, Action<double> onProgress)
        {
            return new Progress<double>(value =>
            {
                if (!IsCurrent(scope)) return;
                onProgress(value);
            });
        }

        public void Dispose()
        {
            _currentRunId++;
            CancelAndDisposeCurrentSource();
        }

        private void CancelAndDisposeCurrentSource()
        {
            try
            {
                _currentCts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Best-effort soft cancel; disposing still completes lifecycle cleanup.
            }

            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    public readonly record struct SolverRunScope(int RunId, CancellationToken CancellationToken);
}
