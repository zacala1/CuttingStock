using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Persistence;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class MainViewModel
    {
        [RelayCommand]
        private void SaveScenario()
        {
            var options = TryParseOptions();
            if (options == null) return;

            var path = _dialog.PromptSaveFile(
                "시나리오 저장",
                "1D 시나리오 (*.cstock1d.json)|*.cstock1d.json|JSON (*.json)|*.json",
                $"1D시나리오_{DateTime.Now:yyyyMMdd_HHmmss}.cstock1d.json");
            if (path == null) return;

            try
            {
                var scenario = new ScenarioService.Scenario1D
                {
                    Stocks = Stocks.Select(s => new ScenarioService.Stock1DDto { Length = s.Length, Quantity = s.Quantity }).ToList(),
                    Orders = Orders.Select(o => new ScenarioService.Order1DDto { Length = o.Length, Quantity = o.Quantity }).ToList(),
                    Parameters = new ScenarioService.Options1DDto
                    {
                        Alpha = options.Alpha, Beta = options.Beta,
                        Gamma = options.Gamma, Delta = options.Delta, Kerf = options.Kerf,
                        UsageOrder = options.UsageOrder, EnableWelding = options.EnableWelding,
                    },
                };
                ScenarioService.Save1D(path, scenario);
                StatusText = $"저장됨: {System.IO.Path.GetFileName(path)}";
                ScenarioSaved?.Invoke(this, path);
                _dialog.ShowInfo("저장 완료", $"시나리오를 저장했습니다.\n{path}");
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"시나리오 저장 오류: {ex.Message}");
            }
        }

        [RelayCommand]
        private void LoadScenario()
        {
            var path = _dialog.PromptOpenFile(
                "시나리오 불러오기",
                "1D 시나리오 (*.cstock1d.json)|*.cstock1d.json|JSON (*.json)|*.json|모든 파일 (*.*)|*.*");
            if (path == null) return;

            LoadScenarioFromPath(path);
        }

        public bool LoadScenarioFromPath(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            try
            {
                var scenario = ScenarioService.Load1D(path);
                Stocks.Clear();
                foreach (var s in scenario.Stocks)
                    Stocks.Add(new StockRow { Length = s.Length, Quantity = s.Quantity });
                Orders.Clear();
                foreach (var o in scenario.Orders)
                    Orders.Add(new OrderRow { Length = o.Length, Quantity = o.Quantity });

                var p = scenario.Parameters;
                AlphaText = p.Alpha.ToString(CultureInfo.InvariantCulture);
                BetaText = p.Beta.ToString(CultureInfo.InvariantCulture);
                GammaText = p.Gamma.ToString(CultureInfo.InvariantCulture);
                DeltaText = p.Delta.ToString(CultureInfo.InvariantCulture);
                KerfText = p.Kerf.ToString(CultureInfo.InvariantCulture);
                UsageOrderIndex = p.UsageOrder == StockUsageOrder.SmallToLarge ? 0 : 1;
                EnableWelding = p.EnableWelding;
                StatusText = $"불러옴: {System.IO.Path.GetFileName(path)}";
                ScenarioLoaded?.Invoke(this, path);
                return true;
            }
            catch (Exception ex)
            {
                _dialog.ShowError("오류", $"시나리오 불러오기 오류: {ex.Message}");
                return false;
            }
        }
    }
}
