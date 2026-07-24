using System.IO;
using CuttingStock.UI.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class MainViewModelScenarioTests
    {
        private FakeDialogService _dialog = null!;
        private MainViewModel _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _dialog = new FakeDialogService();
            _vm = new MainViewModel(_dialog);
        }

        [TearDown]
        public void TearDown()
        {
            _vm.Dispose();
        }

        [Test]
        public void SaveThenLoadScenario_RestoresAllInputs()
        {
            var path = Path.Combine(Path.GetTempPath(), $"vm-test-{System.Guid.NewGuid():N}.cstock1d.json");
            try
            {
                _vm.Stocks.Add(new StockRow { Length = 9000, Quantity = 7 });
                _vm.Orders.Add(new OrderRow { Length = 3500, Quantity = 4 });
                _vm.AlphaText = "1.5";
                _vm.BetaText = "800";
                _vm.GammaText = "200";
                _vm.DeltaText = "150";
                _vm.KerfText = "3";
                _vm.UsageOrderIndex = 1;
                _vm.EnableWelding = true;

                _dialog.SavePathResponses.Enqueue(path);
                _vm.SaveScenarioCommand.Execute(null);
                File.Exists(path).Should().BeTrue();

                var dialog2 = new FakeDialogService();
                using var vm2 = new MainViewModel(dialog2);
                dialog2.OpenPathResponses.Enqueue(path);
                vm2.LoadScenarioCommand.Execute(null);

                vm2.Stocks.Should().HaveCount(1);
                vm2.Stocks[0].Length.Should().Be(9000);
                vm2.Stocks[0].Quantity.Should().Be(7);
                vm2.Orders.Should().HaveCount(1);
                vm2.Orders[0].Length.Should().Be(3500);
                vm2.AlphaText.Should().Be("1.5");
                vm2.BetaText.Should().Be("800");
                vm2.KerfText.Should().Be("3");
                vm2.UsageOrderIndex.Should().Be(1);
                vm2.EnableWelding.Should().BeTrue();
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void SaveScenario_FiresScenarioSavedEvent()
        {
            var path = Path.Combine(Path.GetTempPath(), $"vm-evt-{System.Guid.NewGuid():N}.cstock1d.json");
            string? capturedPath = null;
            _vm.ScenarioSaved += (_, p) => capturedPath = p;
            try
            {
                _dialog.SavePathResponses.Enqueue(path);
                _vm.SaveScenarioCommand.Execute(null);

                capturedPath.Should().Be(path);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void LoadScenario_FiresScenarioLoadedEvent()
        {
            var path = Path.Combine(Path.GetTempPath(), $"vm-evt-{System.Guid.NewGuid():N}.cstock1d.json");
            try
            {
                _dialog.SavePathResponses.Enqueue(path);
                _vm.SaveScenarioCommand.Execute(null);

                string? capturedPath = null;
                _vm.ScenarioLoaded += (_, p) => capturedPath = p;
                _vm.IsRunning = true;
                _vm.CanCancel = true;

                _vm.LoadScenarioFromPath(path).Should().BeTrue();

                capturedPath.Should().Be(path);
                _vm.IsRunning.Should().BeTrue();
                _vm.CanCancel.Should().BeFalse();
                _vm.CalculateCommand.CanExecute(null).Should().BeFalse();
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
