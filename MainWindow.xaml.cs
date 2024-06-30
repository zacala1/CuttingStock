using CuttingStock.Domain;
using CuttingStock.Models;
using System.Collections.ObjectModel;
using System.Windows;
using static CuttingStock.Domain.CuttingStockOptimizer;

namespace CuttingStock
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<RebarStock> Stocks { get; set; }
        public ObservableCollection<Order> Orders { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            Stocks = new ObservableCollection<RebarStock>();
            Orders = new ObservableCollection<Order>();
            stockGrid.ItemsSource = Stocks;
            orderGrid.ItemsSource = Orders;
            DataContext = this;
        }

        private void AddStock_Click(object sender, RoutedEventArgs e)
        {
            Stocks.Add(new RebarStock());
        }

        private void AddOrder_Click(object sender, RoutedEventArgs e)
        {
            Orders.Add(new Order());
        }

        private void Calculate_Click(object sender, RoutedEventArgs e)
        {
            if (Stocks.Count == 0 || Orders.Count == 0)
            {
                MessageBox.Show("재고와 주문을 입력해주세요.");
                return;
            }

            var stock = Stocks.ToList();
            var orders = Orders.Select(o => new Order(o.Length, o.Quantity)).ToList();

            float alpha = 1;  // 자투리 1mm당 비용
            float beta = 500;  // 용접 1회당 비용
            int gamma = 100;  // 사용 가능한 자투리의 최소 길이
            int delta = 100;  // 용접 가능한 조각의 최소 길이

            var (result, leftover, welds) = CuttingStockOptimizer.OptimizeCutting(stock, orders, StockUsageOrder.SmallToLarge);

            string output = "절단 결과:\n";
            foreach (var (stockLength, cuts) in result)
            {
                output += $"{stockLength}mm 재고에서 절단: [{string.Join(", ", cuts)}]\n";
            }

            output += $"\n사용 가능한 자투리: [{string.Join(", ", leftover)}]\n";
            output += $"총 용접 횟수: {welds}\n";

            var totalWaste = result.Sum(r => r.StockLength - r.Cuts.Sum()) - leftover.Sum();
            output += $"총 자투리: {totalWaste}mm\n";
            output += $"총 비용: {totalWaste * alpha + welds * beta}";

            resultTextBox.Text = output;
        }
    }
}