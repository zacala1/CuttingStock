using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.Core.Domain;
using CuttingStock.UI.ViewModels;

namespace CuttingStock.UI.Tests
{
    /// <summary>
    /// Unit tests for <see cref="MainViewModel"/>. Uses <see cref="FakeDialogService"/>
    /// to script user responses so the ViewModel can be driven without a live WPF
    /// dispatcher.
    /// </summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class MainViewModelTests
    {
        private FakeDialogService _dialog = null!;
        private MainViewModel _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _dialog = new FakeDialogService();
            _vm = new MainViewModel(_dialog);
        }

        // ─── Re-entrancy gate ───────────────────────────────────────

        [Test]
        public void CalculateCommand_CanExecute_FalseWhileRunning()
        {
            _vm.CalculateCommand.CanExecute(null).Should().BeTrue();
            _vm.IsRunning = true;
            _vm.CalculateCommand.CanExecute(null).Should().BeFalse("spam-click guard");
        }

        [Test]
        public void CompareAlgorithmsCommand_CanExecute_FalseWhileRunning()
        {
            _vm.CompareAlgorithmsCommand.CanExecute(null).Should().BeTrue();
            _vm.IsRunning = true;
            _vm.CompareAlgorithmsCommand.CanExecute(null).Should().BeFalse();
        }

        // ─── Calculate validation ───────────────────────────────────

        [Test]
        public async Task Calculate_EmptyStocks_ShowsWarning()
        {
            _vm.Orders.Add(new OrderRow { Length = 1000, Quantity = 1 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _dialog.Messages.Should().ContainSingle(m => m.Severity == "warning");
        }

        [Test]
        public async Task Calculate_EmptyOrders_ShowsWarning()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 5 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _dialog.Messages.Should().ContainSingle(m => m.Severity == "warning");
        }

        [Test]
        public async Task Calculate_InvalidAlphaText_ShowsWarningRejectsRun()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 5 });
            _vm.Orders.Add(new OrderRow { Length = 5000, Quantity = 3 });
            _vm.AlphaText = "not-a-number";

            await _vm.CalculateCommand.ExecuteAsync(null);

            _dialog.Messages.Should().Contain(m => m.Severity == "warning" && m.Title == "입력 오류");
            _vm.HasSingleResult.Should().BeFalse();
        }

        [Test]
        public async Task Calculate_HappyPath_SetsHasSingleResultAndVisualization()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 3 });
            _vm.Orders.Add(new OrderRow { Length = 4000, Quantity = 2 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _vm.HasSingleResult.Should().BeTrue();
            _vm.VisualizationRows.Should().NotBeNull();
            _vm.LegendItems.Should().NotBeNull();
            _vm.ResultText.Should().NotBeNullOrWhiteSpace();
            _dialog.Messages.Should().Contain(m => m.Severity == "info" && m.Title == "최적화 완료");
        }

        // ─── Scenario round-trip ────────────────────────────────────

        [Test]
        public void SaveThenLoadScenario_RestoresAllInputs()
        {
            var path = Path.Combine(Path.GetTempPath(), $"vm-test-{System.Guid.NewGuid():N}.cstock1d.json");
            try
            {
                // Populate VM state.
                _vm.Stocks.Add(new StockRow { Length = 9000, Quantity = 7 });
                _vm.Orders.Add(new OrderRow { Length = 3500, Quantity = 4 });
                _vm.AlphaText = "1.5";
                _vm.BetaText  = "800";
                _vm.GammaText = "200";
                _vm.DeltaText = "150";
                _vm.KerfText  = "3";
                _vm.UsageOrderIndex = 1;
                _vm.EnableWelding = true;

                _dialog.SavePathResponses.Enqueue(path);
                _vm.SaveScenarioCommand.Execute(null);
                File.Exists(path).Should().BeTrue();

                // Reset VM and load.
                var vm2 = new MainViewModel(new FakeDialogService());
                var fakeDialog2 = (FakeDialogService)vm2.GetType()
                    .GetField("_dialog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(vm2)!;
                fakeDialog2.OpenPathResponses.Enqueue(path);
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
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // ─── DeleteSelected* helpers ────────────────────────────────

        [Test]
        public void DeleteSelectedStocks_EmptySelection_ShowsInfoNotice()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 1 });

            _vm.DeleteSelectedStocks(System.Array.Empty<StockRow>());

            _dialog.Messages.Should().ContainSingle(m => m.Severity == "info" && m.Title == "알림");
            _vm.Stocks.Should().HaveCount(1);
        }

        [Test]
        public void DeleteSelectedStocks_SingleRow_NoConfirm()
        {
            var row = new StockRow { Length = 12000, Quantity = 1 };
            _vm.Stocks.Add(row);

            _vm.DeleteSelectedStocks(new[] { row });

            _vm.Stocks.Should().BeEmpty();
            _dialog.Messages.Should().NotContain(m => m.Severity == "confirm");
        }

        [Test]
        public void DeleteSelectedStocks_MultipleRows_AsksConfirm()
        {
            var rows = new[]
            {
                new StockRow { Length = 12000, Quantity = 1 },
                new StockRow { Length = 9000,  Quantity = 2 },
            };
            foreach (var r in rows) _vm.Stocks.Add(r);
            _dialog.ConfirmResponses.Enqueue(true);

            _vm.DeleteSelectedStocks(rows);

            _dialog.Messages.Should().Contain(m => m.Severity == "confirm");
            _vm.Stocks.Should().BeEmpty();
        }

        [Test]
        public void DeleteSelectedStocks_MultipleRows_UserDeclines()
        {
            var rows = new[]
            {
                new StockRow { Length = 12000, Quantity = 1 },
                new StockRow { Length = 9000,  Quantity = 2 },
            };
            foreach (var r in rows) _vm.Stocks.Add(r);
            _dialog.ConfirmResponses.Enqueue(false);

            _vm.DeleteSelectedStocks(rows);

            _vm.Stocks.Should().HaveCount(2);
        }

        // ─── LoadExample ─────────────────────────────────────────────

        [Test]
        public void LoadExample_PopulatesStocksAndOrders()
        {
            _vm.LoadExampleCommand.Execute(null);

            _vm.Stocks.Should().NotBeEmpty();
            _vm.Orders.Should().NotBeEmpty();
            _dialog.Messages.Should().Contain(m => m.Title == "예제 로드");
        }

        // ─── UsageOrder default ──────────────────────────────────────

        [Test]
        public void UsageOrderIndex_DefaultsToZeroSmallToLarge()
        {
            _vm.UsageOrderIndex.Should().Be(0);  // matches StockUsageOrder.SmallToLarge
        }

        // ─── Cancel command + soft-cancel ────────────────────────────

        [Test]
        public void CancelCommand_CanExecute_FalseWhenIdle()
        {
            _vm.CancelCommand.CanExecute(null).Should().BeFalse("cancel only valid mid-run");
        }

        [Test]
        public void CancelCommand_CanExecute_TrueWhenCanCancelSet()
        {
            _vm.CanCancel = true;
            _vm.CancelCommand.CanExecute(null).Should().BeTrue();
        }

        [Test]
        public void Cancel_WhenRunning_FreesUiAndSetsCancelledText()
        {
            _vm.IsRunning = true;
            _vm.CanCancel = true;

            _vm.CancelCommand.Execute(null);

            _vm.IsRunning.Should().BeFalse();
            _vm.CanCancel.Should().BeFalse();
            _vm.ProgressText.Should().Contain("취소");
        }

        // ─── StatusText default ──────────────────────────────────────

        [Test]
        public void StatusText_DefaultsToReady()
        {
            _vm.StatusText.Should().Be("준비됨");
        }

        // ─── StatusText / ScenarioSaved-Loaded wiring ───────────────

        [Test]
        public async Task Calculate_HappyPath_UpdatesStatusText()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 3 });
            _vm.Orders.Add(new OrderRow { Length = 4000, Quantity = 2 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _vm.StatusText.Should().NotBe("준비됨");
            _vm.StatusText.Should().Contain("완료");
        }

        [Test]
        public void LoadExample_UpdatesStatusText()
        {
            _vm.LoadExampleCommand.Execute(null);
            _vm.StatusText.Should().Contain("예제");
        }

        [Test]
        public void Cancel_UpdatesStatusText()
        {
            _vm.IsRunning = true;
            _vm.CanCancel = true;
            _vm.CancelCommand.Execute(null);
            _vm.StatusText.Should().Be("취소됨");
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
                if (File.Exists(path)) File.Delete(path);
            }
        }

        // ─── CTS lifecycle ───────────────────────────────────────────

        [Test]
        public async Task Calculate_Twice_DisposesOldCts()
        {
            // First run — completes synchronously enough that _currentCts is set.
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 3 });
            _vm.Orders.Add(new OrderRow { Length = 4000, Quantity = 2 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            // Capture the CTS reference via reflection so we can verify it's disposed
            // when the second run replaces it.
            var ctsField = typeof(MainViewModel).GetField("_currentCts",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var firstCts = (System.Threading.CancellationTokenSource?)ctsField.GetValue(_vm);
            firstCts.Should().NotBeNull();

            await _vm.CalculateCommand.ExecuteAsync(null);

            // Old CTS should now be disposed (calling Cancel throws ObjectDisposedException).
            var act = () => firstCts!.Cancel();
            act.Should().Throw<ObjectDisposedException>("old CTS must be disposed when a new run starts");
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

                _dialog.OpenPathResponses.Enqueue(path);
                _vm.LoadScenarioCommand.Execute(null);

                capturedPath.Should().Be(path);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
