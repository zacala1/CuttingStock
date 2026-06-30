using System;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Persistence;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class TwoDViewModel
    {
        [RelayCommand]
        private void SaveScenario()
        {
            var options = TryParseOptions();
            if (options == null) return;
            var path = _dialog.PromptSaveFile(
                "시나리오 저장",
                "2D 시나리오 (*.cstock2d.json)|*.cstock2d.json|JSON (*.json)|*.json",
                $"2D시나리오_{DateTime.Now:yyyyMMdd_HHmmss}.cstock2d.json");
            if (path == null) return;
            try
            {
                var scenario = new ScenarioService.Scenario2D
                {
                    Sheets = Sheets.Select(s => new ScenarioService.Sheet2DDto
                    {
                        Width = s.Width, Height = s.Height, Quantity = s.Quantity,
                    }).ToList(),
                    Orders = Orders.Select(o => new ScenarioService.Order2DDto
                    {
                        Width = o.Width, Height = o.Height, Quantity = o.Quantity, AllowRotation = o.AllowRotation,
                    }).ToList(),
                    Options = new ScenarioService.Options2DDto
                    {
                        Kerf = options.Kerf, Trim = options.Trim, AlphaArea = options.AlphaArea,
                        AllowRotation = options.AllowRotation, Stage = options.Stage,
                        TimeLimitMs = options.TimeLimitMs, UsageOrder = options.UsageOrder,
                    },
                };
                ScenarioService.Save2D(path, scenario);
                StatusText = $"저장됨: {System.IO.Path.GetFileName(path)}";
                _dialog.ShowInfo("저장 완료", $"시나리오를 저장했습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"시나리오 저장 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void LoadScenario()
        {
            var path = _dialog.PromptOpenFile(
                "시나리오 불러오기",
                "2D 시나리오 (*.cstock2d.json)|*.cstock2d.json|JSON (*.json)|*.json|모든 파일 (*.*)|*.*");
            if (path == null) return;
            try
            {
                var scenario = ScenarioService.Load2D(path);
                Sheets.Clear();
                foreach (var s in scenario.Sheets)
                    Sheets.Add(new SheetRow { Width = s.Width, Height = s.Height, Quantity = s.Quantity });
                Orders.Clear();
                foreach (var o in scenario.Orders)
                    Orders.Add(new RectOrderRow { Width = o.Width, Height = o.Height, Quantity = o.Quantity, AllowRotation = o.AllowRotation });

                var o2 = scenario.Options;
                KerfText = o2.Kerf.ToString(CultureInfo.InvariantCulture);
                TrimText = o2.Trim.ToString(CultureInfo.InvariantCulture);
                AlphaAreaText = o2.AlphaArea.ToString(CultureInfo.InvariantCulture);
                TimeLimitText = o2.TimeLimitMs.ToString(CultureInfo.InvariantCulture);
                AllowRotation = o2.AllowRotation;
                StageIndex = o2.Stage == 3 ? 1 : 0;
                UsageOrderIndex = o2.UsageOrder == StockUsageOrder.SmallToLarge ? 0 : 1;
                StatusText = $"불러옴: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"시나리오 불러오기 오류: {ex.Message}"); }
        }
    }
}
