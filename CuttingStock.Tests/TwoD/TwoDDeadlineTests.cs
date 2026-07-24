using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Algorithms.Utilities;
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
    }
}
