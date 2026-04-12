using System.IO;
using System.Text;
using ClosedXML.Excel;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;

namespace CuttingStock.UI.Services
{
    /// <summary>
    /// Static helpers that export optimisation results to CSV / Excel.
    /// Extracted from MainWindow so the code-behind stays focused on UI wiring.
    /// </summary>
    public static class ExportService
    {
        // ───────────────────────────────────────────────────────
        //  Single optimisation result
        // ───────────────────────────────────────────────────────

        public static void ExportSingleResultToCsv(
            string filename,
            ICuttingSolver optimizer,
            SolverResult result,
            SolverOptions parameters)
        {
            using var writer = new StreamWriter(filename, false, Encoding.UTF8);

            writer.WriteLine("철근 절단 최적화 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"알고리즘,{CsvEscape(optimizer.Name)}");
            writer.WriteLine($"시간 복잡도,{CsvEscape(optimizer.TimeComplexity)}");
            writer.WriteLine();

            writer.WriteLine("파라미터");
            writer.WriteLine($"Alpha (자투리 비용),{parameters.Alpha}");
            writer.WriteLine($"Beta (용접 비용),{parameters.Beta}");
            writer.WriteLine($"Gamma (재사용 최소),{parameters.Gamma}");
            writer.WriteLine($"Delta (용접 최소),{parameters.Delta}");
            writer.WriteLine($"재고 사용 순서,{parameters.UsageOrder}");
            writer.WriteLine();

            writer.WriteLine("결과 요약");
            writer.WriteLine($"총 비용,{result.TotalCost}원");
            writer.WriteLine($"낭비 길이,{result.WasteLength}mm");
            writer.WriteLine($"재고 사용,{result.StockUsed}개");
            writer.WriteLine($"재료 효율,{result.MaterialEfficiency:F2}%");
            writer.WriteLine($"실행 시간,{result.ExecutionTimeMs:F3}ms");
            writer.WriteLine();

            writer.WriteLine("절단 계획");
            writer.WriteLine("번호,재고 길이,절단 개수,자투리");
            for (int i = 0; i < result.CuttingPlans.Count; i++)
            {
                var plan = result.CuttingPlans[i];
                writer.WriteLine($"{i + 1},{plan.StockLength},{plan.Cuts.Count},{plan.Leftover}");
            }
        }

        public static void ExportSingleResultToExcel(
            string filename,
            ICuttingSolver optimizer,
            SolverResult result,
            SolverOptions parameters)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("최적화 결과");

            int row = 1;

            worksheet.Cell(row, 1).Value = "철근 절단 최적화 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 1).Value = "알고리즘:";
            worksheet.Cell(row++, 2).Value = optimizer.Name;
            worksheet.Cell(row, 1).Value = "시간 복잡도:";
            worksheet.Cell(row++, 2).Value = optimizer.TimeComplexity;
            row++;

            worksheet.Cell(row, 1).Value = "파라미터";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "Alpha (자투리 비용):";
            worksheet.Cell(row++, 2).Value = parameters.Alpha;
            worksheet.Cell(row, 1).Value = "Beta (용접 비용):";
            worksheet.Cell(row++, 2).Value = parameters.Beta;
            worksheet.Cell(row, 1).Value = "Gamma (재사용 최소):";
            worksheet.Cell(row++, 2).Value = parameters.Gamma;
            worksheet.Cell(row, 1).Value = "Delta (용접 최소):";
            worksheet.Cell(row++, 2).Value = parameters.Delta;
            worksheet.Cell(row, 1).Value = "재고 사용 순서:";
            worksheet.Cell(row++, 2).Value = parameters.UsageOrder.ToString();
            row++;

            worksheet.Cell(row, 1).Value = "결과 요약";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "총 비용:";
            worksheet.Cell(row++, 2).Value = $"{result.TotalCost}원";
            worksheet.Cell(row, 1).Value = "낭비 길이:";
            worksheet.Cell(row++, 2).Value = $"{result.WasteLength}mm";
            worksheet.Cell(row, 1).Value = "재고 사용:";
            worksheet.Cell(row++, 2).Value = $"{result.StockUsed}개";
            worksheet.Cell(row, 1).Value = "재료 효율:";
            worksheet.Cell(row++, 2).Value = $"{result.MaterialEfficiency:F2}%";
            worksheet.Cell(row, 1).Value = "실행 시간:";
            worksheet.Cell(row++, 2).Value = $"{result.ExecutionTimeMs:F3}ms";
            row++;

            worksheet.Cell(row, 1).Value = "절단 계획";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "번호";
            worksheet.Cell(row, 2).Value = "재고 길이";
            worksheet.Cell(row, 3).Value = "절단 개수";
            worksheet.Cell(row, 4).Value = "자투리";
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;

            for (int i = 0; i < result.CuttingPlans.Count; i++)
            {
                var plan = result.CuttingPlans[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = plan.StockLength;
                worksheet.Cell(row, 3).Value = plan.Cuts.Count;
                worksheet.Cell(row, 4).Value = plan.Leftover;
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filename);
        }

        // ───────────────────────────────────────────────────────
        //  Comparison results
        // ───────────────────────────────────────────────────────

        public static void ExportComparisonResultsToCsv(
            string filename,
            IEnumerable<ComparisonResult> comparisonResults)
        {
            using var writer = new StreamWriter(filename, false, Encoding.UTF8);

            writer.WriteLine("알고리즘 비교 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();

            writer.WriteLine("알고리즘,총 비용,낭비(mm),재고 사용,효율(%),실행 시간(ms),순위");

            foreach (var result in comparisonResults.OrderBy(r => r.Rank))
            {
                writer.WriteLine($"{CsvEscape(result.AlgorithmName)},{result.TotalCost},{result.WasteLength}," +
                               $"{result.StockUsed},{result.MaterialEfficiency:F2},{result.ExecutionTimeMs:F3},{result.Rank}");
            }
        }

        public static void ExportComparisonResultsToExcel(
            string filename,
            IEnumerable<ComparisonResult> comparisonResults)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("알고리즘 비교");

            int row = 1;

            worksheet.Cell(row, 1).Value = "알고리즘 비교 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row++;

            worksheet.Cell(row, 1).Value = "알고리즘";
            worksheet.Cell(row, 2).Value = "총 비용";
            worksheet.Cell(row, 3).Value = "낭비(mm)";
            worksheet.Cell(row, 4).Value = "재고 사용";
            worksheet.Cell(row, 5).Value = "효율(%)";
            worksheet.Cell(row, 6).Value = "실행 시간(ms)";
            worksheet.Cell(row, 7).Value = "순위";
            worksheet.Range(row, 1, row, 7).Style.Font.Bold = true;
            row++;

            foreach (var result in comparisonResults.OrderBy(r => r.Rank))
            {
                worksheet.Cell(row, 1).Value = result.AlgorithmName;
                worksheet.Cell(row, 2).Value = result.TotalCost;
                worksheet.Cell(row, 3).Value = result.WasteLength;
                worksheet.Cell(row, 4).Value = result.StockUsed;
                worksheet.Cell(row, 5).Value = result.MaterialEfficiency;
                worksheet.Cell(row, 6).Value = result.ExecutionTimeMs;
                worksheet.Cell(row, 7).Value = result.Rank;

                if (result.Rank == 1)
                {
                    worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightGreen;
                }

                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filename);
        }

        // ───────────────────────────────────────────────────────
        //  Helpers
        // ───────────────────────────────────────────────────────

        private static string CsvEscape(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
