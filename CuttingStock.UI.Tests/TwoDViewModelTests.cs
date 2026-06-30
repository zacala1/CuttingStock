using System.Threading.Tasks;
using NUnit.Framework;
using FluentAssertions;
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
        }
    }
}
