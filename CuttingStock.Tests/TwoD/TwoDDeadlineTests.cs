using System.Diagnostics;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.TwoD
{
    [TestFixture]
    [Category("Architecture")]
    public class TwoDDeadlineTests
    {
        [Test]
        public void Deadline_RemainingMilliseconds_UsesElapsedFromSolverStart()
        {
            long elapsed = 250;
            var deadline = TwoDDeadline.FromElapsedProvider(1000, () => elapsed);

            deadline.ElapsedMilliseconds.Should().Be(250);
            deadline.RemainingMilliseconds.Should().Be(750);

            elapsed = 900;
            deadline.RemainingMilliseconds.Should().Be(100);
        }

        [Test]
        public void Deadline_IsExpired_DoesNotResetAfterWarmStart()
        {
            long elapsed = 1000;
            var deadline = TwoDDeadline.FromElapsedProvider(1000, () => elapsed);

            deadline.IsExpired.Should().BeTrue();
            deadline.RemainingMilliseconds.Should().Be(0);
        }

        [Test]
        public void Deadline_PhaseEndMilliseconds_SplitsAbsoluteBudget()
        {
            var deadline = TwoDDeadline.FromElapsedProvider(9000, () => 0);

            deadline.PhaseEndMilliseconds(1, 2).Should().Be(4500);
            deadline.PhaseEndMilliseconds(2, 3).Should().Be(6000);
        }

        [Test]
        public void Deadline_IsPast_UsesAbsolutePhaseEnd()
        {
            long elapsed = 4999;
            var deadline = TwoDDeadline.FromElapsedProvider(10000, () => elapsed);
            long pricingEnd = deadline.PhaseEndMilliseconds(1, 2);

            deadline.IsPast(pricingEnd).Should().BeFalse();

            elapsed = 5000;
            deadline.IsPast(pricingEnd).Should().BeTrue();
        }

        [Test]
        public void Deadline_TryGetRemainingMilliseconds_NeverExtendsAbsoluteBudget()
        {
            long elapsed = 9500;
            var deadline = TwoDDeadline.FromElapsedProvider(10000, () => elapsed);

            deadline.TryGetRemainingMilliseconds(out long remaining).Should().BeTrue();
            remaining.Should().Be(500);

            elapsed = 10000;
            deadline.TryGetRemainingMilliseconds(out remaining).Should().BeFalse();
            remaining.Should().Be(0);
        }

        [Test]
        public void StagedMip_IntegerMasterDeadlineGate_SkipsSolverAfterAbsoluteDeadline()
        {
            var deadline = TwoDDeadline.FromElapsedProvider(1000, () => 1000);
            int calls = 0;

            bool solved = StagedMipGuillotineSolver.TryRunIntegerMaster(
                deadline,
                _ =>
                {
                    calls++;
                    return true;
                });

            solved.Should().BeFalse();
            calls.Should().Be(0);
        }

        [Test]
        public void StagedMip_IntegerMasterDeadlineGate_ForwardsExactRemainingBudget()
        {
            var deadline = TwoDDeadline.FromElapsedProvider(1000, () => 625);
            long? receivedBudget = null;

            bool solved = StagedMipGuillotineSolver.TryRunIntegerMaster(
                deadline,
                remaining =>
                {
                    receivedBudget = remaining;
                    return true;
                });

            solved.Should().BeTrue();
            receivedBudget.Should().Be(375);
        }

        [Test]
        public void Deadline_HasLessThanReserve_ModelsDiversificationTailStop()
        {
            long elapsed = 9000;
            var deadline = TwoDDeadline.FromElapsedProvider(10000, () => elapsed);

            deadline.HasLessThanReserve(1000).Should().BeFalse();

            elapsed = 9001;
            deadline.HasLessThanReserve(1000).Should().BeTrue();
        }

        [Test]
        public void FinishWithWarmStart_AfterResidualCancellation_RestoresSuccessState()
        {
            var sheets = new List<Sheet> { new(100, 100, 1) };
            var orders = new List<RectOrder> { new(50, 50, 1) };
            var options = new SolverOptions2D();
            var warm = new ShelfGuillotineSolver().Solve(sheets, orders, options);
            var result = new SolverResult2D
            {
                AlgorithmName = "Column Generation",
                Success = false,
                ErrorMessage = "Residual coverage failed: Time limit reached.",
            };

            var finished = ColumnGeneration2DSolver.FinishWithWarmStart(
                result,
                warm,
                sheets,
                orders,
                options,
                Stopwatch.StartNew());

            finished.Success.Should().BeTrue();
            finished.ErrorMessage.Should().BeNull();
            SolverUtils2D.ValidateSuccessfulResult(sheets, orders, options, finished)
                .Should().BeNull();
        }

        [Test]
        public void ExpandOrders_WhenCancellationRequested_StopsDuringQuantityExpansion()
        {
            int checks = 0;
            var orders = new List<RectOrder> { new(10, 10, 1_000_000) };

            Action expand = () => TwoDInputPreprocessor.ExpandOrders(
                orders,
                globalAllowRotation: true,
                shouldStop: () => ++checks >= 2);

            expand.Should().Throw<OperationCanceledException>();
            checks.Should().Be(2);
        }

        [Test]
        public void SortWithCancellation_WhenCancellationRequested_StopsDuringComparison()
        {
            int checks = 0;
            var values = Enumerable.Range(0, 128).Reverse().ToList();

            Action sort = () => ShelfGuillotineSolver.SortWithCancellation(
                values,
                static (left, right) => left.CompareTo(right),
                shouldStop: () => ++checks >= 2);

            sort.Should().Throw<OperationCanceledException>();
            checks.Should().Be(2);
        }

        [Test]
        public void PeriodicCancellationCheck_WhenCancellationRequested_ThrowsOnNextCadence()
        {
            int checks = 0;

            Action iterate = () =>
            {
                for (int index = 0; index < 1_000; index++)
                {
                    ShelfGuillotineSolver.ThrowIfStoppedPeriodically(
                        index,
                        shouldStop: () => ++checks >= 2);
                }
            };

            iterate.Should().Throw<OperationCanceledException>();
            checks.Should().Be(2);
        }
    }
}
