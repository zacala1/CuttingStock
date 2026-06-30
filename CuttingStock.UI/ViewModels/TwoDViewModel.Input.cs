using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class TwoDViewModel
    {
        [RelayCommand] private void AddSheet() => Sheets.Add(new SheetRow());
        [RelayCommand] private void AddRectOrder() => Orders.Add(new RectOrderRow());

        [RelayCommand]
        private void ClearAll()
        {
            if (!_dialog.Confirm("전체 초기화", "시트와 주문 데이터를 모두 삭제하시겠습니까?")) return;
            Sheets.Clear();
            Orders.Clear();
            ReportText = string.Empty;
            CompareText = string.Empty;
            CompareRows.Clear();
            HasSingleResult = false;
            HasComparisonResults = false;
            _lastResult = null;
        }

        [RelayCommand]
        private void LoadExample()
        {
            Sheets.Clear();
            Orders.Clear();
            Sheets.Add(new SheetRow { Width = 2440, Height = 1220, Quantity = 5 });
            Sheets.Add(new SheetRow { Width = 1220, Height = 1220, Quantity = 5 });
            Orders.Add(new RectOrderRow { Width = 600,  Height = 400, Quantity = 6 });
            Orders.Add(new RectOrderRow { Width = 800,  Height = 300, Quantity = 4 });
            Orders.Add(new RectOrderRow { Width = 300,  Height = 300, Quantity = 8 });
            Orders.Add(new RectOrderRow { Width = 1200, Height = 500, Quantity = 2 });
            StatusText = "2D 예제 데이터 로드됨";
        }

        public void DeleteSelectedSheets(IEnumerable<SheetRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0) { _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요."); return; }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Sheets.Remove(item);
        }

        public void DeleteSelectedOrders(IEnumerable<RectOrderRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0) { _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요."); return; }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Orders.Remove(item);
        }
    }
}
