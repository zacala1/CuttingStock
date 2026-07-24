using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
using CuttingStock.UI.Services;
using CuttingStock.UI.ViewModels;

namespace CuttingStock.UI.Tests
{
    /// <summary>
    /// Unit tests for <see cref="TwoDViewModel"/>. Same pattern as
    /// <see cref="MainViewModelTests"/> — fake dialog, scripted responses.
    /// </summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class TwoDViewModelTests
    {
        private FakeDialogService _dialog = null!;
        private TwoDViewModel _vm = null!;

        [SetUp]
        public void SetUp()
        {
            _dialog = new FakeDialogService();
            _vm = new TwoDViewModel(_dialog);
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
            _vm.CalculateCommand.CanExecute(null).Should().BeFalse();
        }

        [Test]
        public void CompareCommand_CanExecute_FalseWhileRunning()
        {
            _vm.CompareCommand.CanExecute(null).Should().BeTrue();
            _vm.IsRunning = true;
            _vm.CompareCommand.CanExecute(null).Should().BeFalse();
        }

        // ─── Defaults ───────────────────────────────────────────────

        [Test]
        public void UsageOrderIndex_DefaultsToOneLargeToSmall()
        {
            _vm.UsageOrderIndex.Should().Be(1);  // matches StockUsageOrder.LargeToSmall
        }

        [Test]
        public void AllowRotation_DefaultsToTrue()
        {
            _vm.AllowRotation.Should().BeTrue();
        }

        [Test]
        public void StageIndex_DefaultsToZeroForTwoStage()
        {
            _vm.StageIndex.Should().Be(0);
        }

        [Test]
        public void StageIndex_AdvisorySolverPreservesThreeStageSelection()
        {
            _vm.AlgorithmIndex = 0;

            _vm.StageIndex = 1;

            _vm.StageIndex.Should().Be(1);
            _vm.CanConfigureStage.Should().BeTrue();
        }

        // ─── Calculate validation ───────────────────────────────────

        [Test]
        public async Task Calculate_EmptySheets_ShowsWarning()
        {
            _vm.Orders.Add(new RectOrderRow { Width = 100, Height = 100, Quantity = 1 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _dialog.Messages.Should().ContainSingle(m => m.Severity == "warning");
            _vm.HasSingleResult.Should().BeFalse();
        }

        [Test]
        public async Task Calculate_InvalidKerf_ShowsWarning()
        {
            _vm.Sheets.Add(new SheetRow { Width = 1000, Height = 1000, Quantity = 1 });
            _vm.Orders.Add(new RectOrderRow { Width = 100, Height = 100, Quantity = 1 });
            _vm.KerfText = "negative-please";

            await _vm.CalculateCommand.ExecuteAsync(null);

            _dialog.Messages.Should().Contain(m => m.Severity == "warning" && m.Title == "입력 오류");
        }

        [Test]
        public async Task Calculate_HappyPath_SetsHasSingleResult()
        {
            _vm.Sheets.Add(new SheetRow { Width = 1000, Height = 1000, Quantity = 1 });
            _vm.Orders.Add(new RectOrderRow { Width = 200, Height = 200, Quantity = 4 });

            await _vm.CalculateCommand.ExecuteAsync(null);

            _vm.HasSingleResult.Should().BeTrue();
            _vm.LastResult.Should().NotBeNull();
            _vm.ReportText.Should().NotBeNullOrWhiteSpace();
            _vm.RenderProjection.Should().NotBeNull();
            _vm.RenderProjection!.Patterns.Should().NotBeEmpty();
        }

        [Test]
        public async Task ExportToCsv_AfterCalculateUsesTwoDResultAndOptions()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                $"cutting-stock-2d-export-{Guid.NewGuid():N}.csv");

            try
            {
                _vm.Sheets.Add(new SheetRow { Width = 1000, Height = 1000, Quantity = 1 });
                _vm.Orders.Add(new RectOrderRow { Width = 200, Height = 200, Quantity = 4 });
                _vm.TrimText = "5";
                await _vm.CalculateCommand.ExecuteAsync(null);
                _dialog.SavePathResponses.Enqueue(path);

                _vm.ExportToCsvCommand.Execute(null);

                string csv = File.ReadAllText(path);
                csv.Should().Contain("2D 절단 최적화 결과");
                csv.Should().Contain("Trim,5");
                csv.Should().Contain($"알고리즘,{_vm.SelectedSolverDescriptor.Name}");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public async Task Compare_HappyPath_SetsChartAndWinnerRenderProjections()
        {
            _vm.Sheets.Add(new SheetRow { Width = 1000, Height = 1000, Quantity = 2 });
            _vm.Orders.Add(new RectOrderRow { Width = 200, Height = 200, Quantity = 4 });
            _vm.TimeLimitText = "100";

            await _vm.CompareCommand.ExecuteAsync(null);

            _vm.HasComparisonResults.Should().BeTrue();
            _vm.ChartProjection.Should().NotBe(TwoDChartProjection.Empty);
            _vm.ChartProjection.Labels.Should().NotBeEmpty();
            _vm.RenderProjection.Should().NotBeNull();
            _vm.RenderProjection!.AlgorithmName.Should().Be(
                _vm.CompareRows.Single(row => row.Rank == 1).AlgorithmName);
        }

        [Test]
        public async Task Compare_AllSolversFail_ClearsPreviousWinnerProjection()
        {
            _vm.Sheets.Add(new SheetRow { Width = 1000, Height = 1000, Quantity = 1 });
            _vm.Orders.Add(new RectOrderRow { Width = 200, Height = 200, Quantity = 1 });
            await _vm.CalculateCommand.ExecuteAsync(null);
            _vm.HasSingleResult.Should().BeTrue();

            _vm.Orders.Clear();
            _vm.Orders.Add(new RectOrderRow { Width = 2000, Height = 2000, Quantity = 1 });
            _vm.TimeLimitText = "100";

            await _vm.CompareCommand.ExecuteAsync(null);

            _vm.HasComparisonResults.Should().BeTrue();
            _vm.CompareRows.Should().OnlyContain(row => !row.Success);
            _vm.HasSingleResult.Should().BeFalse();
            _vm.LastResult.Should().BeNull();
            _vm.LastOptions.Should().BeNull();
            _vm.RenderProjection.Should().BeNull();
            _vm.ChartProjection.Should().Be(TwoDChartProjection.Empty);
        }

        // ─── LoadExample ─────────────────────────────────────────────

        [Test]
        public void LoadExample_PopulatesSheetsAndOrders()
        {
            _vm.LoadExampleCommand.Execute(null);

            _vm.Sheets.Should().NotBeEmpty();
            _vm.Orders.Should().NotBeEmpty();
        }

        // ─── ClearAll requires confirmation ─────────────────────────

        [Test]
        public void ClearAll_UserDeclines_KeepsData()
        {
            _vm.LoadExampleCommand.Execute(null);
            int sheetCountBefore = _vm.Sheets.Count;
            _dialog.ConfirmResponses.Enqueue(false);

            _vm.ClearAllCommand.Execute(null);

            _vm.Sheets.Should().HaveCount(sheetCountBefore);
        }

        [Test]
        public void ClearAll_UserConfirms_WipesData()
        {
            _vm.LoadExampleCommand.Execute(null);
            _dialog.ConfirmResponses.Enqueue(true);

            _vm.ClearAllCommand.Execute(null);

            _vm.Sheets.Should().BeEmpty();
            _vm.Orders.Should().BeEmpty();
            _vm.LastResult.Should().BeNull();
            _vm.LastOptions.Should().BeNull();
            _vm.RenderProjection.Should().BeNull();
            _vm.ChartProjection.Should().Be(TwoDChartProjection.Empty);
        }
    }
}
