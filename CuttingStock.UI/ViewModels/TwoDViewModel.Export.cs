using System;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class TwoDViewModel
    {
        [RelayCommand]
        private void ExportToCsv()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장", "CSV 파일 (*.csv)|*.csv",
                $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResult2DToCsv(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            if (_lastResult == null || _lastSolver == null || _lastOptions == null)
            { _dialog.ShowWarning("내보내기 불가", "먼저 최적화를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장", "Excel 파일 (*.xlsx)|*.xlsx",
                $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportSingleResult2DToExcel(path, _lastSolver, _lastResult, _lastOptions);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportCompareToCsv()
        {
            if (CompareRows.Count == 0)
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("CSV 저장", "CSV 파일 (*.csv)|*.csv",
                $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            if (path == null) return;
            try
            {
                ExportService.ExportComparison2DResultsToCsv(path, CompareRows);
                _dialog.ShowInfo("저장 완료", $"CSV 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }

        [RelayCommand]
        private void ExportCompareToExcel()
        {
            if (CompareRows.Count == 0)
            { _dialog.ShowWarning("내보내기 불가", "먼저 알고리즘 비교를 실행해주세요."); return; }
            var path = _dialog.PromptSaveFile("Excel 저장", "Excel 파일 (*.xlsx)|*.xlsx",
                $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            if (path == null) return;
            try
            {
                ExportService.ExportComparison2DResultsToExcel(path, CompareRows);
                _dialog.ShowInfo("저장 완료", $"Excel 파일로 저장되었습니다.\n{path}");
            }
            catch (Exception ex) { _dialog.ShowError("오류", $"내보내기 오류: {ex.Message}"); }
        }
    }
}
