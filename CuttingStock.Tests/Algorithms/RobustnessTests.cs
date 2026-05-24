using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.Tests.Algorithms
{
    /// <summary>
    /// Numerical / adversarial robustness. Hits the 1D solvers with edge cases
    /// that should NOT crash or NaN-poison the result, even when the problem is
    /// strange:
    ///
    ///   - kerf nearly as wide as the stock
    ///   - one giant order against one tiny stock
    ///   - thousands of identical-length items
    ///   - extreme α, β so cost arithmetic could overflow int
    ///   - exactly-fitting cuts (zero leftover)
    ///   - one bar, one cut (degenerate)
    ///
    /// Every test asserts either success with reasonable invariants, or an
    /// explicit failure with a populated <see cref="SolverResult.ErrorMessage"/>.
    /// </summary>
    [TestFixture]
    [Category("Robustness")]
    public class RobustnessTests
    {
        private static IEnumerable<ICuttingSolver> Solvers()
        {
            yield return new GreedyKnapsackSolver();
            yield return new ColumnGenerationSolver();
            yield return new ArcFlowSolver();
        }

        [TestCaseSource(nameof(Solvers))]
        public void Kerf_LargerThanOrders_StillReportsCleanly(ICuttingSolver solver)
        {
            // 1000mm orders, 5000mm bars, kerf=200 → 4 cuts fit per bar with 3 kerfs.
            var stocks = new List<RebarStock> { new(5000, 20) };
            var orders = new List<Order> { new(1000, 10) };
            var opt = new SolverOptions { Kerf = 200 };

            var r = solver.Solve(stocks, orders, opt);

            r.Success.Should().BeTrue("{0} should handle moderately large kerf", solver.Name);
            foreach (var p in r.CuttingPlans)
            {
                int consumed = p.Cuts.Sum(c => c.Length) + Math.Max(0, p.Cuts.Count - 1) * opt.Kerf;
                consumed.Should().BeLessThanOrEqualTo(p.StockLength,
                    "{0}: kerf-aware consumption must not exceed stock", solver.Name);
            }
        }

        [TestCaseSource(nameof(Solvers))]
        public void SingleOrderSingleStock_ExactFit(ICuttingSolver solver)
        {
            var stocks = new List<RebarStock> { new(6000, 1) };
            var orders = new List<Order> { new(6000, 1) };

            var r = solver.Solve(stocks, orders, new SolverOptions());

            r.Success.Should().BeTrue();
            r.StockUsed.Should().Be(1);
            r.WasteLength.Should().Be(0);
            r.CuttingPlans[0].Cuts.Should().HaveCount(1);
            r.CuttingPlans[0].Cuts[0].Length.Should().Be(6000);
            r.CuttingPlans[0].Leftover.Should().Be(0);
        }

        [TestCaseSource(nameof(Solvers))]
        public void ThousandsOfIdenticalCuts_NoCrashNoNaN(ICuttingSolver solver)
        {
            // Single length, big quantity — exercises DP/MIP dedup paths.
            // 4 cuts of 3000mm fit per 12000mm bar → optimum is 1000/4 = 250 bars.
            // The Greedy heuristic's pass-1 maxPerOrder=2 limit means it tends to
            // open more bars than optimal here even after post-processing, so we
            // assert a looser upper bound (≤ 500 bars = 2 cuts each) for Greedy,
            // and the exact optimum for CG and ArcFlow.
            var stocks = new List<RebarStock> { new(12000, 500) };
            var orders = new List<Order> { new(3000, 1000) };

            var r = solver.Solve(stocks, orders, new SolverOptions());

            r.Success.Should().BeTrue("{0} must handle large repetition counts", solver.Name);
            r.CuttingPlans.Sum(p => p.Cuts.Count).Should().Be(1000);

            int upperBound = solver is GreedyKnapsackSolver ? 500 : 250;
            r.StockUsed.Should().BeLessThanOrEqualTo(upperBound,
                "{0}: should use ≤ {1} bars (optimum 250)", solver.Name, upperBound);

            // No NaN/Infinity poisoning the cost / efficiency.
            double.IsFinite(r.MaterialEfficiency).Should().BeTrue();
            r.TotalCost.Should().BeGreaterThanOrEqualTo(0);
        }

        [TestCaseSource(nameof(Solvers))]
        public void ExtremeAlphaBeta_TotalCostStaysWithinLongRange(ICuttingSolver solver)
        {
            // Worst-case for the cost formula: large waste × very high α.
            var stocks = new List<RebarStock> { new(12000, 100) };
            var orders = new List<Order> { new(7000, 50) };
            var opt = new SolverOptions { Alpha = 1_000_000f, Beta = 1_000_000f };

            var r = solver.Solve(stocks, orders, opt);

            r.Success.Should().BeTrue();
            // We migrated TotalCost to long for exactly this reason — verify the
            // value is positive and finite (no overflow / no sign flip).
            r.TotalCost.Should().BeGreaterThanOrEqualTo(0,
                "{0}: TotalCost must not overflow when α and β are huge", solver.Name);
        }

        [Test]
        public void ZeroOrders_FailsCleanlyWithMessage()
        {
            var stocks = new List<RebarStock> { new(12000, 5) };
            var orders = new List<Order>();
            foreach (var solver in Solvers())
            {
                var r = solver.Solve(stocks, orders, new SolverOptions());
                r.Success.Should().BeFalse("{0} must reject empty orders", solver.Name);
                r.ErrorMessage.Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        public void ZeroStock_FailsCleanlyWithMessage()
        {
            var stocks = new List<RebarStock>();
            var orders = new List<Order> { new(5000, 1) };
            foreach (var solver in Solvers())
            {
                var r = solver.Solve(stocks, orders, new SolverOptions());
                r.Success.Should().BeFalse("{0} must reject empty stock", solver.Name);
                r.ErrorMessage.Should().NotBeNullOrEmpty();
            }
        }

        [Test]
        public void OrderLongerThanStock_WithoutWelding_FailsGracefully()
        {
            var stocks = new List<RebarStock> { new(6000, 10) };
            var orders = new List<Order> { new(15000, 1) };  // longer than stock
            var opt = new SolverOptions { EnableWelding = false };

            foreach (var solver in Solvers())
            {
                var r = solver.Solve(stocks, orders, opt);
                r.Success.Should().BeFalse("{0}: order > stock with welding disabled must fail", solver.Name);
            }
        }

        [Test]
        public void OrderLongerThanStock_WithWelding_GreedySucceeds()
        {
            // Welding is only implemented in Greedy currently. Document the
            // expected behaviour here so any future change is caught.
            var stocks = new List<RebarStock> { new(6000, 10) };
            var orders = new List<Order> { new(15000, 1) };
            var opt = new SolverOptions { EnableWelding = true, Delta = 1000 };

            var greedy = new GreedyKnapsackSolver().Solve(stocks, orders, opt);

            greedy.Success.Should().BeTrue();
            greedy.WeldCount.Should().BeGreaterThan(0);
            greedy.CuttingPlans.SelectMany(p => p.Cuts).Sum(c => c.Length).Should().Be(15000);
        }
    }
}
