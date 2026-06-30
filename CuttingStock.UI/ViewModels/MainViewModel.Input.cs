using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.UI.ViewModels
{
    public sealed partial class MainViewModel
    {
        [RelayCommand]
        private void AddStock() => Stocks.Add(new StockRow { Length = 12000, Quantity = 1 });

        [RelayCommand]
        private void AddOrder() => Orders.Add(new OrderRow { Length = 5000, Quantity = 1 });

        [RelayCommand]
        private void ClearAll()
        {
            if (!_dialog.Confirm("전체 초기화", "재고와 주문 데이터를 모두 삭제하시겠습니까?")) return;
            Stocks.Clear();
            Orders.Clear();
        }

        [RelayCommand]
        private void LoadExample()
        {
            Stocks.Clear();
            Orders.Clear();
            Stocks.Add(new StockRow { Length = 12000, Quantity = 20 });
            Orders.Add(new OrderRow { Length = 5000, Quantity = 10 });
            Orders.Add(new OrderRow { Length = 4000, Quantity = 15 });
            Orders.Add(new OrderRow { Length = 3000, Quantity = 12 });
            Orders.Add(new OrderRow { Length = 2000, Quantity = 8 });
            StatusText = "예제 데이터 로드됨";
            _dialog.ShowInfo("예제 로드",
                "예제 데이터를 로드했습니다.\n재고: 12000mm × 20개\n주문: 5000mm×10, 4000mm×15, 3000mm×12, 2000mm×8");
        }

        /// <summary>Removes the supplied rows from <see cref="Stocks"/>.</summary>
        public void DeleteSelectedStocks(IEnumerable<StockRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0)
            {
                _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요.");
                return;
            }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Stocks.Remove(item);
        }

        /// <summary>Removes the supplied rows from <see cref="Orders"/>.</summary>
        public void DeleteSelectedOrders(IEnumerable<OrderRow> selected)
        {
            var list = selected.ToList();
            if (list.Count == 0)
            {
                _dialog.ShowInfo("알림", "삭제할 항목을 선택해주세요.");
                return;
            }
            if (list.Count > 1 && !_dialog.Confirm("선택 삭제", $"{list.Count}개의 행을 삭제하시겠습니까?")) return;
            foreach (var item in list) Orders.Remove(item);
        }

        /// <summary>
        /// Snapshot of the most recent successful single solve — the View uses
        /// this to render the bar visualization. Null until Calculate succeeds.
        /// </summary>
        public SolverResult? LastResult => _lastResult;

        /// <summary>True if the last single solve succeeded with at least one plan.</summary>
        public bool HasVisualization => _lastResult is { Success: true, CuttingPlans.Count: > 0 };
    }
}
