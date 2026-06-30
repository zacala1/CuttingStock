using System.Globalization;
using CuttingStock.Core.Domain;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class MainViewModel
    {
        partial void OnAlgorithmIndexChanged(int value)
        {
            CoerceUnsupportedOptions();
            RefreshSelectedSolverProperties();
        }

        partial void OnEnableWeldingChanged(bool value)
        {
            if (value && !CanConfigureWelding)
                EnableWelding = false;
        }

        private SolverOptions? TryParseOptions()
        {
            if (!float.TryParse(AlphaText, NumberStyles.Float, CultureInfo.InvariantCulture, out float alpha) || alpha < 0)
            { _dialog.ShowWarning("입력 오류", "Alpha 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!float.TryParse(BetaText, NumberStyles.Float, CultureInfo.InvariantCulture, out float beta) || beta < 0)
            { _dialog.ShowWarning("입력 오류", "Beta 값을 올바르게 입력해주세요. (0 이상의 숫자)"); return null; }
            if (!int.TryParse(GammaText, out int gamma) || gamma < 0)
            { _dialog.ShowWarning("입력 오류", "Gamma 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }
            if (!int.TryParse(DeltaText, out int delta) || delta <= 0)
            { _dialog.ShowWarning("입력 오류", "Delta 값을 올바르게 입력해주세요. (1 이상의 정수)"); return null; }
            if (!int.TryParse(KerfText, out int kerf) || kerf < 0)
            { _dialog.ShowWarning("입력 오류", "Kerf 값을 올바르게 입력해주세요. (0 이상의 정수)"); return null; }

            return new SolverOptions
            {
                Alpha = alpha, Beta = beta, Gamma = gamma, Delta = delta, Kerf = kerf,
                UsageOrder = UsageOrderIndex == 0 ? StockUsageOrder.SmallToLarge : StockUsageOrder.LargeToSmall,
                EnableWelding = EnableWelding,
            };
        }

        private void CoerceUnsupportedOptions()
        {
            if (!CanConfigureWelding)
                EnableWelding = false;
        }

        private void RefreshSelectedSolverProperties()
        {
            OnPropertyChanged(nameof(SelectedSolverDescriptor));
            OnPropertyChanged(nameof(SelectedSolverDescription));
            OnPropertyChanged(nameof(SelectedSolverTimeComplexity));
            OnPropertyChanged(nameof(SelectedSolverCapabilityText));
            OnPropertyChanged(nameof(SelectedSolverAdvancedNotes));
            OnPropertyChanged(nameof(CanConfigureWelding));
            OnPropertyChanged(nameof(WeldingOptionTip));
        }

        protected override void OnRunStateChanged()
        {
            CalculateCommand.NotifyCanExecuteChanged();
            CompareAlgorithmsCommand.NotifyCanExecuteChanged();
        }
    }
}
