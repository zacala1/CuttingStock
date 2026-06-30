using System;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class MainViewModel
    {
        [RelayCommand]
        private void ExportToCsv()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장",
                "CSV 파일 (*.csv)|*.csv",
                $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResultToCsv(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장",
                "Excel 파일 (*.xlsx)|*.xlsx",
                $"최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResultToExcel(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportComparisonToCsv()
        {
            if (!ComparisonResults.Any())
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장",
                "CSV 파일 (*.csv)|*.csv",
                $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportComparisonResultsToCsv(path, ComparisonResults);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportComparisonToExcel()
        {
            if (!ComparisonResults.Any())
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장",
                "Excel 파일 (*.xlsx)|*.xlsx",
                $"알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportComparisonResultsToExcel(path, ComparisonResults);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }
    }
}
