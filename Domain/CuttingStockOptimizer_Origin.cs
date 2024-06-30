using CuttingStock.Models;

namespace CuttingStock.Domain
{
    public class CuttingStockOptimizer_Origin
    {
        /// <summary>
        /// 주어진 재고와 주문에 대해 최적의 절단 계획을 수립하는 메인 함수
        /// </summary>
        /// <param name="stock">사용 가능한 재고 목록</param>
        /// <param name="orders">처리해야 할 주문 목록</param>
        /// <param name="alpha">자투리 비용 계수</param>
        /// <param name="beta">용접 비용 계수</param>
        /// <param name="gamma">사용 가능한 자투리의 최소 길이</param>
        /// <param name="delta">용접 가능한 최소 길이</param>
        /// <returns>절단 결과, 사용 가능한 자투리 목록, 총 용접 횟수</returns>
        public static (List<(int StockLength, List<int> Cuts)> Result, List<int> Leftover, int Welds) OptimizeCutting(
            List<RebarStock> stock, List<Order> orders, float alpha, float beta, int gamma, int delta)
        {
            var result = new List<(int StockLength, List<int> Cuts)>();
            var leftover = new List<int>();
            var welds = 0;

            // 모든 재고에 대해 반복
            foreach (var s in stock)
            {
                for (int i = 0; i < s.Quantity; i++)
                {
                    var (cut, cutLeftover) = FindBestCut(s.Length, orders, alpha, beta, delta);
                    if (cut.Any())
                    {
                        result.Add((s.Length, cut));
                        // 사용 가능한 자투리인 경우 저장
                        if (cutLeftover >= gamma)
                            leftover.Add(cutLeftover);
                        // 용접 횟수 계산 (원래 주문 길이보다 짧게 잘린 경우 용접이 필요)
                        welds += cut.Count(c => c < orders.First(o => o.Length == c).Length);
                        // 남은 주문 수량 업데이트
                        orders = orders.Select(o => new Order(o.Length, o.Quantity - cut.Count(c => c == o.Length)))
                            .Where(o => o.Quantity > 0).ToList();
                    }
                }
            }

            return (result, leftover, welds);
        }

        /// <summary>
        /// 주어진 길이에 대해 최적의 절단 방법을 찾는 재귀 함수.
        /// </summary>
        /// <param name="remainingLength">남은 재고 길이</param>
        /// <param name="remainingOrders">남은 주문 목록</param>
        /// <param name="alpha">자투리 비용 계수</param>
        /// <param name="beta">용접 비용 계수</param>
        /// <param name="delta">용접 가능한 최소 길이</param>
        /// <returns>최적의 절단 리스트와 남은 자투리 길이</returns>
        private static (List<int> Cut, int Leftover) FindBestCut(int remainingLength, List<Order> remainingOrders, float alpha, float beta, int delta)
        {
            if (!remainingOrders.Any())
                return (new List<int>(), remainingLength);

            var bestCut = new List<int>();
            var bestLeftover = remainingLength;
            var bestWelds = 0;

            for (int i = 0; i < remainingOrders.Count; i++)
            {
                var orderLength = remainingOrders[i].Length;
                var orderQuantity = remainingOrders[i].Quantity;
                if (orderQuantity == 0)
                    continue;

                // 전체 길이 사용
                if (orderLength <= remainingLength)
                {
                    var newRemaining = remainingLength - orderLength;
                    var newOrders = new List<Order>(remainingOrders);
                    newOrders[i] = new Order(orderLength, orderQuantity - 1);
                    var (cut, leftover) = FindBestCut(newRemaining, newOrders, alpha, beta, delta);
                    var totalLeftover = leftover + (remainingLength - cut.Sum() - leftover);
                    if (totalLeftover < bestLeftover)
                    {
                        bestCut = new List<int> { orderLength };
                        bestCut.AddRange(cut);
                        bestLeftover = totalLeftover;
                        bestWelds = 0;
                    }
                }

                // 분할 사용
                if (orderLength > delta && remainingLength > delta)
                {
                    var splitLength = Math.Min(orderLength - delta, remainingLength - delta);
                    var newRemaining = remainingLength - splitLength;
                    var newOrders = new List<Order>(remainingOrders);
                    newOrders[i] = new Order(orderLength - splitLength, orderQuantity);
                    newOrders.Add(new Order(splitLength, 1));
                    var (cut, leftover) = FindBestCut(newRemaining, newOrders, alpha, beta, delta);
                    var totalLeftover = leftover + (remainingLength - cut.Sum() - leftover);
                    if (totalLeftover + beta / alpha < bestLeftover)
                    {
                        bestCut = new List<int> { splitLength };
                        bestCut.AddRange(cut);
                        bestLeftover = totalLeftover;
                        bestWelds = 1;
                    }
                }
            }

            return (bestCut, bestLeftover);
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
            var (result, leftover, welds) = OptimizeCutting(stock, orders, alpha, beta, gamma, delta);

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