using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.Tests.Algorithms
{
    [TestFixture]
    [Category("Architecture")]
    public class ColumnGenerationProfileTests
    {
        private static IEnumerable<TestCaseData> CompatibilityVariants()
        {
            yield return new TestCaseData(
                "column-generation",
                typeof(ColumnGenerationSolver),
                "Column Generation (LP)",
                "CG with Simplex master + knapsack DP pricing.");
            yield return new TestCaseData(
                "column-generation-stabilized",
                typeof(StabilizedColumnGenerationSolver),
                "Column Generation (Stabilized LP)",
                "CG with dual-smoothed knapsack pricing and raw-dual fallback.");
            yield return new TestCaseData(
                "column-generation-multicolumn",
                typeof(MultiColumnGenerationSolver),
                "Column Generation (Multi-column LP)",
                "CG that adds multiple improving knapsack pricing columns per iteration.");
            yield return new TestCaseData(
                "column-generation-integer-master",
                typeof(IntegerMasterColumnGenerationSolver),
                "Column Generation (Integer Master)",
                "CG with a generated-column CBC integer master polish.");
        }

        [TestCaseSource(nameof(CompatibilityVariants))]
        public void CompatibilityVariant_ExposesStableCatalogIdentity(
            string key,
            Type solverType,
            string name,
            string description)
        {
            SolverDescriptor descriptor = SolverCatalog.All.Single(candidate => candidate.Key == key);

            ICuttingSolver solver = descriptor.CreateSolver();

            solver.Should().BeOfType(solverType);
            solver.Name.Should().Be(name);
            solver.Description.Should().Be(description);
            descriptor.Name.Should().Be(name);
            descriptor.Description.Should().Be(description);
        }

        [Test]
        public void PredefinedProfiles_DeclareVariantBehavior()
        {
            ColumnGenerationProfile.Standard.Should().BeEquivalentTo(
                new
                {
                    Name = "Column Generation (LP)",
                    Description = "CG with Simplex master + knapsack DP pricing.",
                    UseDualStabilization = false,
                    DualSmoothingFactor = 1.0,
                    MaxColumnsPerIteration = 1,
                    UseIntegerMaster = false,
                    IntegerMasterTimeLimitMs = 0L,
                });
            ColumnGenerationProfile.Stabilized.Should().BeEquivalentTo(
                new
                {
                    Name = "Column Generation (Stabilized LP)",
                    Description = "CG with dual-smoothed knapsack pricing and raw-dual fallback.",
                    UseDualStabilization = true,
                    DualSmoothingFactor = 0.70,
                    MaxColumnsPerIteration = 1,
                    UseIntegerMaster = false,
                    IntegerMasterTimeLimitMs = 0L,
                });
            ColumnGenerationProfile.MultiColumn.Should().BeEquivalentTo(
                new
                {
                    Name = "Column Generation (Multi-column LP)",
                    Description = "CG that adds multiple improving knapsack pricing columns per iteration.",
                    UseDualStabilization = false,
                    DualSmoothingFactor = 1.0,
                    MaxColumnsPerIteration = 4,
                    UseIntegerMaster = false,
                    IntegerMasterTimeLimitMs = 0L,
                });
            ColumnGenerationProfile.IntegerMaster.Should().BeEquivalentTo(
                new
                {
                    Name = "Column Generation (Integer Master)",
                    Description = "CG with a generated-column CBC integer master polish.",
                    UseDualStabilization = false,
                    DualSmoothingFactor = 1.0,
                    MaxColumnsPerIteration = 1,
                    UseIntegerMaster = true,
                    IntegerMasterTimeLimitMs = 5000L,
                });
        }

        [Test]
        public void CompatibilityVariants_UseCanonicalProfiles()
        {
            new ColumnGenerationSolver().Profile.Should().BeSameAs(ColumnGenerationProfile.Standard);
            new StabilizedColumnGenerationSolver().Profile.Should().BeSameAs(ColumnGenerationProfile.Stabilized);
            new MultiColumnGenerationSolver().Profile.Should().BeSameAs(ColumnGenerationProfile.MultiColumn);
            new IntegerMasterColumnGenerationSolver().Profile.Should().BeSameAs(ColumnGenerationProfile.IntegerMaster);
        }

        [TestCase(0.0, 1, 0L, "dualSmoothingFactor")]
        [TestCase(1.01, 1, 0L, "dualSmoothingFactor")]
        [TestCase(1.0, 0, 0L, "maxColumnsPerIteration")]
        [TestCase(1.0, 1, -1L, "integerMasterTimeLimitMs")]
        public void Profile_RejectsInvalidAlgorithmKnobs(
            double dualSmoothingFactor,
            int maxColumnsPerIteration,
            long integerMasterTimeLimitMs,
            string parameterName)
        {
            Action create = () => _ = new ColumnGenerationProfile(
                "Test",
                "Test profile",
                useDualStabilization: false,
                dualSmoothingFactor,
                maxColumnsPerIteration,
                useIntegerMaster: false,
                integerMasterTimeLimitMs);

            create.Should().Throw<ArgumentOutOfRangeException>()
                .Which.ParamName.Should().Be(parameterName);
        }

        [Test]
        public void ProtectedPrimitiveConstructor_RemainsSourceCompatible()
        {
            var solver = new CompatibilityProbeSolver();

            solver.Name.Should().Be("Compatibility probe");
            solver.Description.Should().Be("Protected constructor probe.");
            solver.Profile.MaxColumnsPerIteration.Should().Be(2);
        }

        private sealed class CompatibilityProbeSolver : ColumnGenerationSolver
        {
            public CompatibilityProbeSolver()
                : base(
                    name: "Compatibility probe",
                    description: "Protected constructor probe.",
                    useDualStabilization: false,
                    dualSmoothingFactor: 1.0,
                    maxColumnsPerIteration: 2,
                    useIntegerMaster: false,
                    integerMasterTimeLimitMs: 0)
            {
            }
        }
    }
}
