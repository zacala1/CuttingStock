using System;
using System.IO;
using CuttingStock.Core.Persistence;
using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class ScenarioFileRouteServiceTests
    {
        [TestCase("sample.cstock1d.json", true)]
        [TestCase("sample.cstock2d.json", true)]
        [TestCase("sample.json", true)]
        [TestCase("sample.csv", false)]
        [TestCase("", false)]
        public void IsCandidate_RecognizesScenarioJsonPaths(string path, bool expected)
        {
            ScenarioFileRouteService.IsCandidate(path).Should().Be(expected);
        }

        [Test]
        public void DetectRoute_UsesPersistedSchemaForGenericJson()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"route-{Guid.NewGuid():N}.json");

            try
            {
                ScenarioService.Save2D(path, new ScenarioService.Scenario2D());

                ScenarioFileRouteService.DetectRoute(path)
                    .Should().Be(ScenarioRoute.TwoD);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
