using System;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>
    /// Boundary &amp; edge-value tests for 2D domain types.
    /// </summary>
    [TestFixture]
    public class Domain2DBoundaryTests
    {
        // ----- Sheet -----

        [Test]
        public void Sheet_NegativeWidth_Throws()
        {
            Action act = () => new Sheet(-1, 100, 1);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Sheet_MinimumValid_OneByOne()
        {
            var s = new Sheet(1, 1, 1);
            s.Area.Should().Be(1L);
        }

        [Test]
        public void Sheet_LargeDimensions_NoOverflow()
        {
            // 1m × 1m sheet at micrometer resolution → 10^12 mm² area
            var s = new Sheet(1_000_000, 1_000_000, 1);
            s.Area.Should().Be(1_000_000_000_000L);
        }

        [Test]
        public void Sheet_NotEqualToNull()
        {
            var s = new Sheet(100, 100, 1);
            s.Equals(null).Should().BeFalse();
            (s == null).Should().BeFalse();
            (null == s).Should().BeFalse();
            (s != null).Should().BeTrue();
        }

        [Test]
        public void Sheet_EquatableContract_IsConsistent()
        {
            var a = new Sheet(100, 200, 3);
            var b = new Sheet(100, 200, 3);
            var c = new Sheet(100, 200, 4);
            (a == b).Should().BeTrue();
            (a == c).Should().BeFalse();
            (a != c).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Test]
        public void Sheet_ToString_ContainsDimensions()
        {
            new Sheet(2440, 1220, 5).ToString().Should().Contain("2440").And.Contain("1220").And.Contain("5");
        }

        // ----- RectOrder -----

        [Test]
        public void RectOrder_NegativeQuantity_Throws()
        {
            Action act = () => new RectOrder(100, 100, -1);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void RectOrder_ZeroDimension_Throws()
        {
            Action a1 = () => new RectOrder(0, 100, 1);
            Action a2 = () => new RectOrder(100, 0, 1);
            a1.Should().Throw<ArgumentOutOfRangeException>();
            a2.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void RectOrder_HashCode_DependsOnAllowRotation()
        {
            var a = new RectOrder(100, 200, 3, allowRotation: true);
            var b = new RectOrder(100, 200, 3, allowRotation: false);
            a.GetHashCode().Should().NotBe(b.GetHashCode());
        }

        // ----- SolverOptions2D -----

        [Test]
        public void SolverOptions2D_NegativeTrim_Throws()
        {
            var o = new SolverOptions2D();
            Action act = () => o.Trim = -1;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void SolverOptions2D_NegativeAlphaArea_Throws()
        {
            var o = new SolverOptions2D();
            Action act = () => o.AlphaArea = -0.1f;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void SolverOptions2D_ZeroTimeLimit_Throws()
        {
            var o = new SolverOptions2D();
            Action act = () => o.TimeLimitMs = 0;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void SolverOptions2D_Stage_AcceptsTwoAndThree()
        {
            var o = new SolverOptions2D { Stage = 2 };
            o.Stage.Should().Be(2);
            o.Stage = 3;
            o.Stage.Should().Be(3);
        }

        [Test]
        public void SolverOptions2D_Stage_RejectsOneAndFour()
        {
            var o = new SolverOptions2D();
            Action a1 = () => o.Stage = 1;
            Action a4 = () => o.Stage = 4;
            a1.Should().Throw<ArgumentOutOfRangeException>();
            a4.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ----- CuttingPattern2D / Placement -----

        [Test]
        public void CuttingPattern2D_EfficiencyCalculation()
        {
            var pat = new CuttingPattern2D
            {
                Sheet = new Sheet(100, 100, 1),
                Multiplicity = 1,
                Placements = new() { new() { Width = 50, Height = 50 } },
            };
            pat.UsedArea.Should().Be(2500);
            pat.WasteArea.Should().Be(7500);
            pat.Efficiency.Should().Be(25.0);
        }

        [Test]
        public void Placement_RightAndBottom_AreInclusiveOfSize()
        {
            var p = new Placement { X = 10, Y = 20, Width = 30, Height = 40 };
            p.Right.Should().Be(40);
            p.Bottom.Should().Be(60);
            p.Area.Should().Be(1200);
        }
    }
}
