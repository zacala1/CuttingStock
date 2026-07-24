using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
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

        [TearDown]
        public void TearDown()
        {
            _vm.Dispose();
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

        [Test]
        public async Task Compare_RanksSuccessfulRowsWithoutReplacingSingleResultOptions()
        {
            _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 3 });
            _vm.Orders.Add(new OrderRow { Length = 4000, Quantity = 2 });
            _vm.GammaText = "100";
            await _vm.CalculateCommand.ExecuteAsync(null);
            var singleResultOptions = _vm.LastOptions;

            _vm.GammaText = "999";
            await _vm.CompareAlgorithmsCommand.ExecuteAsync(null);

            _vm.HasComparisonResults.Should().BeTrue();
            var successfulByCost = _vm.ComparisonResults
                .Where(row => row.Success)
                .OrderBy(row => row.TotalCost)
                .ToList();
            successfulByCost.Select(row => row.Rank)
                .Should().Equal(Enumerable.Range(
                    1,
                    successfulByCost.Count));
            successfulByCost.First().Rank.Should().Be(1);
            _vm.ComparisonResults.Where(row => !row.Success)
                .Should().OnlyContain(row => row.Rank == 0);
            _vm.LastOptions.Should().BeSameAs(singleResultOptions);
        }

        [Test]
        public async Task ExportToCsv_AfterCalculateUsesOneDResultAndOptions()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"cutting-stock-1d-export-{Guid.NewGuid():N}.csv");

            try
            {
                _vm.Stocks.Add(new StockRow { Length = 12000, Quantity = 3 });
                _vm.Orders.Add(new OrderRow { Length = 4000, Quantity = 2 });
                _vm.GammaText = "321";
                await _vm.CalculateCommand.ExecuteAsync(null);
                _dialog.SavePathResponses.Enqueue(path);

                _vm.ExportToCsvCommand.Execute(null);

                string csv = File.ReadAllText(path);
                csv.Should().Contain("철근 절단 최적화 결과");
                csv.Should().Contain("Gamma (재사용 최소),321");
                csv.Should().Contain($"알고리즘,{_vm.SelectedSolverDescriptor.Name}");
            }
            finally
            {
                File.Delete(path);
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

        // ─── StatusText wiring ─────────────────────────────────────

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

    }
}
