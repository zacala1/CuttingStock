using System;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.Tests.TwoD
{
    /// <summary>2D 도메인 모델: 검증, 동등성, 해시, 문자열화.</summary>
    [TestFixture]
    public class Domain2DTests
    {
        // ----- Sheet -----

        [Test]
        public void Sheet_Constructor_RejectsNonPositive()
        {
            Action a1 = () => new Sheet(0, 100, 1);
            Action a2 = () => new Sheet(100, 0, 1);
            Action a3 = () => new Sheet(100, 100, 0);
            a1.Should().Throw<ArgumentOutOfRangeException>();
            a2.Should().Throw<ArgumentOutOfRangeException>();
            a3.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void Sheet_Equality_SameValues_AreEqual()
        {
            var a = new Sheet(2440, 1220, 5);
            var b = new Sheet(2440, 1220, 5);
            a.Equals(b).Should().BeTrue();
            (a == b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Test]
        public void Sheet_Area_IsWidthTimesHeight()
        {
            new Sheet(2440, 1220, 1).Area.Should().Be(2440L * 1220L);
        }

        // ----- RectOrder -----

        [Test]
        public void RectOrder_Constructor_RejectsNonPositive()
        {
            Action a = () => new RectOrder(0, 100, 1);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void RectOrder_AllowRotation_DefaultsTrue()
        {
            new RectOrder(100, 200, 1).AllowRotation.Should().BeTrue();
            new RectOrder(100, 200, 1, false).AllowRotation.Should().BeFalse();
        }

        [Test]
        public void RectOrder_Equality_ChecksAllFields()
        {
            new RectOrder(100, 200, 3, true).Equals(new RectOrder(100, 200, 3, true)).Should().BeTrue();
            new RectOrder(100, 200, 3, true).Equals(new RectOrder(100, 200, 3, false)).Should().BeFalse();
        }

        // ----- SolverOptions2D -----

        [Test]
        public void SolverOptions2D_Defaults_AreSane()
        {
            var o = new SolverOptions2D();
            o.Kerf.Should().Be(0);
            o.Trim.Should().Be(0);
            o.AllowRotation.Should().BeTrue();
            o.Stage.Should().BeOneOf(2, 3);
            o.AlphaArea.Should().Be(1f);
            o.TimeLimitMs.Should().BeGreaterThan(0);
        }

        [Test]
        public void SolverOptions2D_Stage_RejectsInvalid()
        {
            var o = new SolverOptions2D();
            Action act = () => o.Stage = 4;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void SolverOptions2D_NegativeKerf_Rejected()
        {
            var o = new SolverOptions2D();
            Action act = () => o.Kerf = -1;
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
