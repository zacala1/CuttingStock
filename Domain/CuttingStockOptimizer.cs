using CuttingStock.Models;

namespace CuttingStock.Domain
{
    public class CuttingStockOptimizer
    {
        public enum StockUsageOrder
        {
            SmallToLarge,
            LargeToSmall
        }

        /// <summary>
        /// 주어진 재고와 주문에 대해 최적의 절단 계획을 수립하는 메인 함수.
        /// 절단 횟수, 자투리가 남는 길이를 최소화.
        /// </summary>
        /// <param name="stock">사용 가능한 재고 목록</param>
        /// <param name="orders">처리해야 할 주문 목록</param>
        /// <param name="usageOrder">재고 사용 순서</param>
        /// <returns>절단 결과, 남은 자투리, 총 절단 횟수</returns>
        public static (List<(int StockLength, List<int> Cuts)> Result, List<int> Leftover, int Cuts) OptimizeCutting(
            List<RebarStock> stock,
            List<Order> orders,
            StockUsageOrder usageOrder)
        {
            var result = new List<(int StockLength, List<int> Cuts)>();
            var leftover = new List<int>();
            var totalCuts = 0;

            // 재고를 선택된 순서로 정렬
            var sortedStock = usageOrder == StockUsageOrder.SmallToLarge
                ? stock.OrderBy(s => s.Length).ToList()
                : stock.OrderByDescending(s => s.Length).ToList();

            // 주문을 길이 내림차순으로 정렬
            var sortedOrders = orders.OrderByDescending(o => o.Length).ToList();

            foreach (var stockItem in sortedStock)
            {
                for (int i = 0; i < stockItem.Quantity; i++)
                {
                    if (!sortedOrders.Any())
                        break;

                    var bestCuts = FindBestCuts(stockItem.Length, sortedOrders);
                    if (bestCuts.Any())
                    {
                        result.Add((stockItem.Length, bestCuts));
                        totalCuts += bestCuts.Count - 1; // 마지막 조각은 자를 필요 없음
                        var remainingLength = stockItem.Length - bestCuts.Sum();
                        if (remainingLength > 0)
                        {
                            leftover.Add(remainingLength);
                        }

                        // 사용된 주문 수량 감소
                        UpdateOrders(sortedOrders, bestCuts);
                    }
                    else
                    {
                        // 더 이상 자를 수 없는 경우 전체를 자투리로 추가
                        leftover.Add(stockItem.Length);
                    }
                }

                if (!sortedOrders.Any())
                    break;
            }

            // 남은 주문이 있다면, 자투리를 활용하여 처리
            if (sortedOrders.Any())
            {
                var additionalResult = ProcessRemainingOrders(sortedOrders, leftover, usageOrder);
                result.AddRange(additionalResult.Result);
                leftover = additionalResult.Leftover;
                totalCuts += additionalResult.Cuts;
            }

            return (result, leftover, totalCuts);
        }

        private static List<int> FindBestCuts(int stockLength, List<Order> orders)
        {
            var dp = new List<int>[stockLength + 1];
            for (int i = 0; i <= stockLength; i++)
            {
                dp[i] = new List<int>();
            }

            for (int i = 1; i <= stockLength; i++)
            {
                foreach (var order in orders)
                {
                    if (order.Length <= i)
                    {
                        var remainingLength = i - order.Length;
                        var newCuts = new List<int>(dp[remainingLength]) { order.Length };
                        if (newCuts.Sum() > dp[i].Sum() || newCuts.Sum() == dp[i].Sum() && newCuts.Count < dp[i].Count)
                        {
                            dp[i] = newCuts;
                        }
                    }
                }
            }

            return dp[stockLength];
        }

        private static void UpdateOrders(List<Order> orders, List<int> cuts)
        {
            foreach (var cut in cuts)
            {
                var order = orders.FirstOrDefault(o => o.Length == cut);
                if (order != default)
                {
                    var index = orders.IndexOf(order);
                    if (order.Quantity > 1)
                    {
                        orders[index] = new Order(order.Length, order.Quantity - 1);
                    }
                    else
                    {
                        orders.RemoveAt(index);
                    }
                }
            }
        }

        private static (List<(int StockLength, List<int> Cuts)> Result, List<int> Leftover, int Cuts) ProcessRemainingOrders(
            List<Order> remainingOrders, List<int> availableLeftovers, StockUsageOrder usageOrder)
        {
            var result = new List<(int StockLength, List<int> Cuts)>();
            var newLeftovers = new List<int>();
            var totalCuts = 0;

            // 사용 가능한 자투리를 선택된 순서로 정렬
            availableLeftovers.Sort(usageOrder == StockUsageOrder.SmallToLarge
                ? (a, b) => a.CompareTo(b)
                : (a, b) => b.CompareTo(a));

            foreach (var leftover in availableLeftovers)
            {
                if (!remainingOrders.Any())
                    break;

                var bestCuts = FindBestCuts(leftover, remainingOrders);
                if (bestCuts.Any())
                {
                    result.Add((leftover, bestCuts));
                    totalCuts += bestCuts.Count - 1;
                    var remainingLength = leftover - bestCuts.Sum();
                    if (remainingLength > 0)
                    {
                        newLeftovers.Add(remainingLength);
                    }

                    // 사용된 주문 수량 감소
                    UpdateOrders(remainingOrders, bestCuts);
                }
                else
                {
                    newLeftovers.Add(leftover);
                }
            }

            // 처리되지 않은 자투리 추가
            newLeftovers.AddRange(availableLeftovers.Where(l => !result.Any(r => r.StockLength == l)));

            return (result, newLeftovers, totalCuts);
        }

        protected static void Example()
        {
            // 예제 데이터 설정
            var stock = new List<RebarStock> { new RebarStock() { Length = 12000, Quantity = 10 } };  // 12000mm 길이의 재고 10개
            var orders = new List<Order> { new Order(5000, 5), new Order(3000, 8), new Order(2000, 6) };  // 주문 목록: (길이, 수량)

            float alpha = 1;  // 자투리 1mm당 비용
            float beta = 500;  // 용접 1회당 비용
            int gamma = 1000;  // 사용 가능한 자투리의 최소 길이
            int delta = 1000;  // 용접 가능한 조각의 최소 길이

            // 최적화 실행
            var (result, leftover, welds) = OptimizeCutting(stock, orders, StockUsageOrder.SmallToLarge);

            // 결과 출력
            Console.WriteLine("절단 결과:");
            foreach (var (stockLength, cuts) in result)
            {
                Console.WriteLine($"{stockLength}mm 재고에서 절단: [{string.Join(", ", cuts)}]");
            }

            Console.WriteLine($"\n사용 가능한 자투리: [{string.Join(", ", leftover)}]");
            Console.WriteLine($"총 용접 횟수: {welds}");

            var totalWaste = result.Sum(r => r.StockLength - r.Cuts.Sum()) - leftover.Sum();
            Console.WriteLine($"총 자투리: {totalWaste}mm");
            Console.WriteLine($"총 비용: {totalWaste * alpha + welds * beta}");
        }
    }
}