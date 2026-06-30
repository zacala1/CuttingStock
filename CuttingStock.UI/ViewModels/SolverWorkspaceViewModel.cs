using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>Shared run-state surface for solver workspace ViewModels.</summary>
    public abstract partial class SolverWorkspaceViewModel : ObservableObject, IDisposable
    {
        private readonly SolverRunLifecycle _runLifecycle = new();
        private bool _disposed;
        private string _statusText = "준비됨";
        private bool _isRunning;
        private double _progressPercent;
        private bool _progressIndeterminate = true;
        private string _progressText = "계산 중...";
        private bool _canCancel;

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                if (SetProperty(ref _isRunning, value))
                    OnRunStateChanged();
            }
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        public bool ProgressIndeterminate
        {
            get => _progressIndeterminate;
            set => SetProperty(ref _progressIndeterminate, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public bool CanCancel
        {
            get => _canCancel;
            set
            {
                if (SetProperty(ref _canCancel, value))
                    CancelCommand.NotifyCanExecuteChanged();
            }
        }

        protected bool CanRunSolver() => !IsRunning;

        protected Task RunSolverAsync(
            string initialProgressText,
            Func<SolverRunScope, Task> executeAsync,
            Action<Exception> onError,
            double initialProgressPercent = 0)
        {
            return _runLifecycle.RunAsync(
                onStarted: () =>
                {
                    IsRunning = true;
                    CanCancel = true;
                    ProgressIndeterminate = true;
                    ProgressPercent = initialProgressPercent;
                    ProgressText = initialProgressText;
                },
                executeAsync: executeAsync,
                onCompleted: () =>
                {
                    IsRunning = false;
                    CanCancel = false;
                },
                onError: onError);
        }

        protected Progress<double> CreateProgress(SolverRunScope scope, Action<double> onProgress) =>
            _runLifecycle.CreateProgress(scope, onProgress);

        protected bool IsCurrent(SolverRunScope scope) => _runLifecycle.IsCurrent(scope);

        protected async Task<SolverComparisonBatch<TSolver, TResult, TRow>> CompareSolversAsync<TDescriptor, TSolver, TOptions, TResult, TRow>(
            SolverRunScope scope,
            IReadOnlyList<TDescriptor> descriptors,
            TOptions options,
            string progressTextPrefix,
            Func<TSolver, IProgress<double>, Task<TResult>> solveAsync,
            Func<TDescriptor, TRow> createSkippedRow,
            Func<TSolver, TResult, TRow> createResultRow,
            Func<TSolver, string> getSolverName,
            Func<TResult, string> getReport)
            where TDescriptor : ISolverDescriptor<TSolver, TOptions>
            where TSolver : class
            where TResult : class
            where TRow : class
        {
            ArgumentNullException.ThrowIfNull(descriptors);
            ArgumentNullException.ThrowIfNull(solveAsync);
            ArgumentNullException.ThrowIfNull(createSkippedRow);
            ArgumentNullException.ThrowIfNull(createResultRow);
            ArgumentNullException.ThrowIfNull(getSolverName);
            ArgumentNullException.ThrowIfNull(getReport);

            var outcomes = new List<SolverComparisonOutcome<TSolver, TResult, TRow>>();

            for (int i = 0; i < descriptors.Count; i++)
            {
                if (!IsCurrent(scope))
                    return new SolverComparisonBatch<TSolver, TResult, TRow>(false, outcomes);

                var descriptor = descriptors[i];
                ProgressText = $"{progressTextPrefix} ({i + 1}/{descriptors.Count} — {descriptor.Name})";
                ProgressIndeterminate = false;
                ProgressPercent = descriptors.Count == 0 ? 0 : i * 100.0 / descriptors.Count;

                var unsupportedReason = descriptor.GetUnsupportedReason(options);
                if (unsupportedReason != null)
                {
                    outcomes.Add(new SolverComparisonOutcome<TSolver, TResult, TRow>(
                        AlgorithmName: descriptor.Name,
                        Row: createSkippedRow(descriptor),
                        Solver: null,
                        Result: null,
                        Detail: $"실행 안 함: {unsupportedReason}"));
                    continue;
                }

                var solver = descriptor.CreateSolver();
                var solverIndex = i;
                var progress = CreateProgress(scope, pct =>
                {
                    double frac = pct <= 1.0 ? pct : pct / 100.0;
                    double overall = (solverIndex + Math.Clamp(frac, 0, 1)) / descriptors.Count * 100.0;
                    ProgressPercent = Math.Clamp(overall, 0, 100);
                });

                var result = await solveAsync(solver, progress);
                if (!IsCurrent(scope))
                    return new SolverComparisonBatch<TSolver, TResult, TRow>(false, outcomes);

                outcomes.Add(new SolverComparisonOutcome<TSolver, TResult, TRow>(
                    AlgorithmName: getSolverName(solver),
                    Row: createResultRow(solver, result),
                    Solver: solver,
                    Result: result,
                    Detail: getReport(result)));
            }

            return new SolverComparisonBatch<TSolver, TResult, TRow>(true, outcomes);
        }

        protected virtual void OnRunStateChanged()
        {
        }

        private bool CanCancelSolver() => CanCancel;

        [RelayCommand(CanExecute = nameof(CanCancelSolver))]
        private void Cancel()
        {
            _runLifecycle.CancelCurrent();
            IsRunning = false;
            CanCancel = false;
            ProgressText = "취소됨";
            StatusText = "취소됨";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _runLifecycle.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
