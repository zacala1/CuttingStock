using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class TwoDViewModel
    {
        partial void OnAlgorithmIndexChanged(int value)
        {
            CoerceUnsupportedOptions();
            RefreshSelectedSolverProperties();
        }

        partial void OnStageIndexChanged(int value)
        {
            if (!SelectedSolverDescriptor.Supports(SolverCapability.EnforcedStage))
                return;

            int selectedStage = value == 1 ? 3 : 2;
            if (!SelectedSolverDescriptor.SupportedStages.Contains(selectedStage))
                StageIndex = SelectedSolverDescriptor.SupportedStages.Contains(2) ? 0 : 1;
        }

        private List<Sheet> BuildSheets()
        {
            var list = new List<Sheet>();
            foreach (var s in Sheets)
                if (s.Width > 0 && s.Height > 0 && s.Quantity > 0)
                    list.Add(new Sheet(s.Width, s.Height, s.Quantity));
            return list;
        }

        private List<RectOrder> BuildOrders()
        {
            var list = new List<RectOrder>();
            foreach (var o in Orders)
                if (o.Width > 0 && o.Height > 0 && o.Quantity > 0)
                    list.Add(new RectOrder(o.Width, o.Height, o.Quantity, o.AllowRotation));
            return list;
        }

        private SolverOptions2D? TryParseOptions()
        {
            if (!int.TryParse(KerfText, out int kerf) || kerf < 0)
            { _dialog.ShowWarning("입력 오류", "Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!int.TryParse(TrimText, out int trim) || trim < 0)
            { _dialog.ShowWarning("입력 오류", "Trim 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!double.TryParse(AlphaAreaText, NumberStyles.Float, CultureInfo.InvariantCulture, out double alpha) || alpha < 0)
            { _dialog.ShowWarning("입력 오류", "AlphaArea 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!int.TryParse(TimeLimitText, out int tl) || tl <= 0)
            { _dialog.ShowWarning("입력 오류", "시간 제한 값을 올바르게 입력해주세요. (1ms 이상의 정수)"); return null; }

            return new SolverOptions2D
            {
                Kerf = kerf, Trim = trim, AlphaArea = (float)alpha,
                AllowRotation = AllowRotation,
                Stage = StageIndex == 1 ? 3 : 2,
                TimeLimitMs = tl,
                UsageOrder = UsageOrderIndex == 0 ? StockUsageOrder.SmallToLarge : StockUsageOrder.LargeToSmall,
            };
        }

        private void CoerceUnsupportedOptions()
        {
            if (!SelectedSolverDescriptor.Supports(SolverCapability.EnforcedStage))
                return;

            int selectedStage = StageIndex == 1 ? 3 : 2;
            if (!SelectedSolverDescriptor.SupportedStages.Contains(selectedStage))
                StageIndex = SelectedSolverDescriptor.SupportedStages.Contains(2) ? 0 : 1;
        }

        private void RefreshSelectedSolverProperties()
        {
            OnPropertyChanged(nameof(SelectedSolverDescriptor));
            OnPropertyChanged(nameof(SelectedSolverDescription));
            OnPropertyChanged(nameof(SelectedSolverCapabilityText));
            OnPropertyChanged(nameof(SelectedSolverAdvancedNotes));
            OnPropertyChanged(nameof(CanConfigureTimeLimit));
            OnPropertyChanged(nameof(CanConfigureStage));
            OnPropertyChanged(nameof(TimeLimitOptionTip));
            OnPropertyChanged(nameof(StageOptionTip));
        }

        protected override void OnRunStateChanged()
        {
            CalculateCommand.NotifyCanExecuteChanged();
            CompareCommand.NotifyCanExecuteChanged();
        }
    }
}
