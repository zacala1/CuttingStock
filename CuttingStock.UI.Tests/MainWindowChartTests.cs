using System.Reflection;
using CuttingStock.Core.Models;
using CuttingStock.UI.ViewModels;
using FluentAssertions;
using LiveChartsCore.SkiaSharpView.WPF;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public sealed class MainWindowChartTests
    {
        [Test]
        public void UpdateCharts_WhenLatestComparisonHasNoSuccess_ClearsPriorSeries()
        {
            var window = new MainWindow();
            try
            {
                var viewModel = window.DataContext.Should().BeOfType<MainViewModel>().Subject;
                viewModel.ComparisonResults.Add(new ComparisonResult
                {
                    AlgorithmName = "Successful",
                    Success = true,
                    TotalCost = 100,
                    MaterialEfficiency = 90,
                    ExecutionTimeMs = 10,
                });
                InvokeUpdateCharts(window);

                GetChart(window, "costChart").Series.Should().NotBeEmpty();
                GetChart(window, "efficiencyChart").Series.Should().NotBeEmpty();
                GetChart(window, "timeChart").Series.Should().NotBeEmpty();

                viewModel.ComparisonResults.Clear();
                viewModel.ComparisonResults.Add(new ComparisonResult
                {
                    AlgorithmName = "Failed",
                    Success = false,
                });
                InvokeUpdateCharts(window);

                GetChart(window, "costChart").Series.Should().BeEmpty();
                GetChart(window, "efficiencyChart").Series.Should().BeEmpty();
                GetChart(window, "timeChart").Series.Should().BeEmpty();
                GetChart(window, "costChart").XAxes.Should().BeEmpty();
                GetChart(window, "efficiencyChart").XAxes.Should().BeEmpty();
                GetChart(window, "timeChart").XAxes.Should().BeEmpty();
            }
            finally
            {
                window.Close();
            }
        }

        private static void InvokeUpdateCharts(MainWindow window)
        {
            typeof(MainWindow)
                .GetMethod("UpdateCharts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, null);
        }

        private static CartesianChart GetChart(MainWindow window, string fieldName)
        {
            return (CartesianChart)typeof(MainWindow)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(window)!;
        }
    }
}
