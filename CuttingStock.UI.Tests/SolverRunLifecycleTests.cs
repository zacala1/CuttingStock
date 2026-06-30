using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class SolverRunLifecycleTests
    {
        [Test]
        public void Begin_InvalidatesPreviousRun()
        {
            using var lifecycle = new SolverRunLifecycle();

            var first = lifecycle.Begin();
            var second = lifecycle.Begin();

            lifecycle.IsCurrent(first).Should().BeFalse();
            lifecycle.IsCurrent(second).Should().BeTrue();
            lifecycle.CurrentRunId.Should().Be(second.RunId);
        }

        [Test]
        public void Begin_CancelsPreviousRunToken()
        {
            using var lifecycle = new SolverRunLifecycle();

            var first = lifecycle.Begin();
            lifecycle.Begin();

            first.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public void CancelCurrent_InvalidatesRunAndCancelsToken()
        {
            using var lifecycle = new SolverRunLifecycle();
            var run = lifecycle.Begin();

            lifecycle.CancelCurrent();

            lifecycle.IsCurrent(run).Should().BeFalse();
            run.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public void Complete_ReturnsFalseForStaleRun()
        {
            using var lifecycle = new SolverRunLifecycle();
            var first = lifecycle.Begin();
            lifecycle.Begin();

            lifecycle.Complete(first).Should().BeFalse();
        }

        [Test]
        public void Complete_InvalidatesCompletedRun()
        {
            using var lifecycle = new SolverRunLifecycle();
            var run = lifecycle.Begin();

            lifecycle.Complete(run).Should().BeTrue();

            lifecycle.IsCurrent(run).Should().BeFalse();
        }

        [Test]
        public void CreateProgress_IgnoresStaleRun()
        {
            using var lifecycle = new SolverRunLifecycle();
            var run = lifecycle.Begin();
            double observed = 0;
            var progress = lifecycle.CreateProgress(run, v => observed = v);

            lifecycle.CancelCurrent();
            ((IProgress<double>)progress).Report(75);

            observed.Should().Be(0);
        }

        [Test]
        public void Dispose_InvalidatesRunAndCancelsToken()
        {
            var lifecycle = new SolverRunLifecycle();
            var run = lifecycle.Begin();

            lifecycle.Dispose();

            lifecycle.IsCurrent(run).Should().BeFalse();
            run.CancellationToken.IsCancellationRequested.Should().BeTrue();
        }

        [Test]
        public async Task RunAsync_CompletesCurrentRun()
        {
            using var lifecycle = new SolverRunLifecycle();
            bool started = false;
            bool executed = false;
            bool completed = false;

            await lifecycle.RunAsync(
                onStarted: () => started = true,
                executeAsync: _ =>
                {
                    executed = true;
                    return Task.CompletedTask;
                },
                onCompleted: () => completed = true);

            started.Should().BeTrue();
            executed.Should().BeTrue();
            completed.Should().BeTrue();
        }

        [Test]
        public async Task RunAsync_RoutesCurrentExceptionToErrorHandler()
        {
            using var lifecycle = new SolverRunLifecycle();
            Exception? observed = null;

            await lifecycle.RunAsync(
                onStarted: () => { },
                executeAsync: _ => throw new InvalidOperationException("boom"),
                onError: ex => observed = ex);

            observed.Should().BeOfType<InvalidOperationException>()
                .Which.Message.Should().Be("boom");
        }

        [Test]
        public async Task RunAsync_IgnoresStaleExceptionAfterCancel()
        {
            using var lifecycle = new SolverRunLifecycle();
            bool completed = false;
            bool errorHandled = false;

            await lifecycle.RunAsync(
                onStarted: () => { },
                executeAsync: _ =>
                {
                    lifecycle.CancelCurrent();
                    throw new InvalidOperationException("stale");
                },
                onCompleted: () => completed = true,
                onError: _ => errorHandled = true);

            completed.Should().BeFalse();
            errorHandled.Should().BeFalse();
        }
    }
}
