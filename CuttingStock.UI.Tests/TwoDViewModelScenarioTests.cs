using System;
using System.IO;
using CuttingStock.UI.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class TwoDViewModelScenarioTests
    {
        [Test]
        public void SaveThenLoadScenario_RestoresDimensionSpecificMapping()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"cutting-stock-2d-{Guid.NewGuid():N}.cstock2d.json");

            try
            {
                var saveDialog = new FakeDialogService();
                saveDialog.SavePathResponses.Enqueue(path);
                string? savedPath = null;
                using (var source = new TwoDViewModel(saveDialog))
                {
                    source.ScenarioSaved += (_, value) => savedPath = value;
                    source.Sheets.Add(new SheetRow { Width = 2440, Height = 1220, Quantity = 3 });
                    source.Orders.Add(new RectOrderRow
                    {
                        Width = 600,
                        Height = 400,
                        Quantity = 5,
                        AllowRotation = false,
                    });
                    source.KerfText = "3";
                    source.TrimText = "7";
                    source.AlphaAreaText = "2.5";
                    source.TimeLimitText = "1234";
                    source.AllowRotation = false;
                    source.StageIndex = 0;
                    source.UsageOrderIndex = 0;

                    source.SaveScenarioCommand.Execute(null);
                }
                savedPath.Should().Be(path);

                var loadDialog = new FakeDialogService();
                using var target = new TwoDViewModel(loadDialog);
                string? loadedPath = null;
                target.ScenarioLoaded += (_, value) => loadedPath = value;
                target.LoadScenarioFromPath(path).Should().BeTrue();

                loadedPath.Should().Be(path);
                target.Sheets.Should().ContainSingle().Which.Should().BeEquivalentTo(
                    new SheetRow { Width = 2440, Height = 1220, Quantity = 3 });
                target.Orders.Should().ContainSingle().Which.Should().BeEquivalentTo(
                    new RectOrderRow
                    {
                        Width = 600,
                        Height = 400,
                        Quantity = 5,
                        AllowRotation = false,
                    });
                target.KerfText.Should().Be("3");
                target.TrimText.Should().Be("7");
                target.AlphaAreaText.Should().Be("2.5");
                target.TimeLimitText.Should().Be("1234");
                target.AllowRotation.Should().BeFalse();
                target.StageIndex.Should().Be(0);
                target.UsageOrderIndex.Should().Be(0);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
