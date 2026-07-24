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
            ExportWorkflow.TryExport(
                _dialog,
                _lastResult != null && _lastSolver != null && _lastOptions != null,
                new ExportDialogRequest(
                    "CSV 저장",
                    "CSV 파일 (*.csv)|*.csv",
                    $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    "CSV 파일로 저장되었습니다.",
                    "먼저 최적화를 실행해주세요."),
                path => ExportService.ExportSingleResult2DToCsv(
                    path,
                    _lastSolver!,
                    _lastResult!,
                    _lastOptions!));
        }

        [RelayCommand]
        private void ExportToExcel()
        {
            ExportWorkflow.TryExport(
                _dialog,
                _lastResult != null && _lastSolver != null && _lastOptions != null,
                new ExportDialogRequest(
                    "Excel 저장",
                    "Excel 파일 (*.xlsx)|*.xlsx",
                    $"2D최적화결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    "Excel 파일로 저장되었습니다.",
                    "먼저 최적화를 실행해주세요."),
                path => ExportService.ExportSingleResult2DToExcel(
                    path,
                    _lastSolver!,
                    _lastResult!,
                    _lastOptions!));
        }

        [RelayCommand]
        private void ExportCompareToCsv()
        {
            ExportWorkflow.TryExport(
                _dialog,
                CompareRows.Count > 0,
                new ExportDialogRequest(
                    "CSV 저장",
                    "CSV 파일 (*.csv)|*.csv",
                    $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                    "CSV 파일로 저장되었습니다.",
                    "먼저 알고리즘 비교를 실행해주세요."),
                path => ExportService.ExportComparison2DResultsToCsv(path, CompareRows));
        }

        [RelayCommand]
        private void ExportCompareToExcel()
        {
            ExportWorkflow.TryExport(
                _dialog,
                CompareRows.Count > 0,
                new ExportDialogRequest(
                    "Excel 저장",
                    "Excel 파일 (*.xlsx)|*.xlsx",
                    $"2D알고리즘비교_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    "Excel 파일로 저장되었습니다.",
                    "먼저 알고리즘 비교를 실행해주세요."),
                path => ExportService.ExportComparison2DResultsToExcel(path, CompareRows));
        }
    }
}
