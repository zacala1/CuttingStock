using CuttingStock.Core.Domain;
using CuttingStock.UI.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class SolverWorkspaceViewModelTests
    {
        [Test]
        public async Task CompareSolversAsync_SkipsUnsupportedDescriptors()
        {
            using var vm = new TestWorkspaceViewModel();
            var unsupported = new TestDescriptor(
                Key: "unsupported",
                Name: "Unsupported",
                IsSupported: false,
                Create: () => throw new InvalidOperationException("should not create"));
            var supported = new TestDescriptor(
                Key: "supported",
                Name: "Supported",
                IsSupported: true,
                Create: () => new TestSolver("Supported"));

            var batch = await vm.CompareAsync(new[] { unsupported, supported });

            batch.Completed.Should().BeTrue();
            batch.Outcomes.Select(o => o.Row.Name).Should().Equal("Unsupported", "Supported");
            batch.Outcomes[0].Row.Success.Should().BeFalse();
            batch.Outcomes[0].Detail.Should().Be("실행 안 함: blocked");
            batch.Outcomes[1].Row.Success.Should().BeTrue();
            batch.Outcomes[1].Result.Should().Be(new TestResult(42));
            vm.ProgressIndeterminate.Should().BeFalse();
        }

        [Test]
        public async Task Cancel_KeepsWorkspaceBusyUntilWorkerActuallyCompletes()
        {
            using var vm = new TestWorkspaceViewModel();
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var runTask = vm.RunUntilReleasedAsync(started, release);
            await started.Task;

            vm.CancelCommand.Execute(null);

            vm.IsRunning.Should().BeTrue();
            vm.CanCancel.Should().BeFalse();
            vm.CanStartRun.Should().BeFalse();

            release.SetResult();
            await runTask;

            vm.IsRunning.Should().BeFalse();
            vm.CanStartRun.Should().BeTrue();
        }

        private sealed class TestWorkspaceViewModel : SolverWorkspaceViewModel
        {
            public bool CanStartRun => CanRunSolver();

            public Task RunUntilReleasedAsync(
                TaskCompletionSource started,
                TaskCompletionSource release) =>
                RunSolverAsync(
                    initialProgressText: "testing",
                    executeAsync: async _ =>
                    {
                        started.SetResult();
                        await release.Task;
                    },
                    onError: ex => throw new InvalidOperationException("unexpected failure", ex));

            public async Task<SolverComparisonBatch<TestSolver, TestResult, TestRow>> CompareAsync(
                IReadOnlyList<TestDescriptor> descriptors)
            {
                SolverComparisonBatch<TestSolver, TestResult, TestRow>? batch = null;

                await RunSolverAsync(
                    initialProgressText: "testing",
                    executeAsync: async scope =>
                    {
                        batch = await CompareSolversAsync<TestDescriptor, TestSolver, TestOptions, TestResult, TestRow>(
                            scope: scope,
                            descriptors: descriptors,
                            options: new TestOptions(),
                            progressTextPrefix: "비교 중...",
                            solveAsync: (solver, progress) =>
                            {
                                ((IProgress<double>)progress).Report(1);
                                return Task.FromResult(new TestResult(42));
                            },
                            createSkippedRow: descriptor => new TestRow(descriptor.Name, false),
                            createResultRow: (solver, result) => new TestRow(solver.Name, true),
                            getSolverName: solver => solver.Name,
                            getReport: result => $"result={result.Value}");
                    },
                    onError: ex => throw new InvalidOperationException("unexpected failure", ex));

                return batch ?? throw new InvalidOperationException("comparison did not run");
            }
        }

        private sealed record TestOptions;

        private sealed record TestResult(int Value);

        private sealed record TestRow(string Name, bool Success);

        private sealed record TestSolver(string Name);

        private sealed record TestDescriptor(
            string Key,
            string Name,
            bool IsSupported,
            Func<TestSolver> Create) : ISolverDescriptor<TestSolver, TestOptions>
        {
            public string DisplayName => Name;
            public string Description => Name;
            public string TimeComplexity => "O(1)";
            public SolverCapability Capabilities => SolverCapability.Heuristic;
            public string CapabilitySummary => "";
            public string AdvancedNotes => "";
            public Func<TestSolver> CreateSolver => Create;

            public bool Supports(SolverCapability capability) => IsSupported;

            public string? GetUnsupportedReason(TestOptions options) =>
                IsSupported ? null : "blocked";
        }
    }
}
