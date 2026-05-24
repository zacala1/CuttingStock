using System.IO;
using System.Text;
using ClosedXML.Excel;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

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

            foreach (var result in RankOrdered(comparisonResults))
            {
                writer.WriteLine($"{CsvEscape(result.AlgorithmName)},{result.TotalCost},{result.WasteLength}," +
                               $"{result.StockUsed},{result.MaterialEfficiency:F2},{result.ExecutionTimeMs:F3}," +
                               $"{(result.Rank > 0 ? result.Rank.ToString() : "-")}");
            }
        }

        // Successful results first, ranked ascending; failed (Rank == 0) sink to the end.
        private static IEnumerable<ComparisonResult> RankOrdered(IEnumerable<ComparisonResult> rows) =>
            rows.OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank);

        private static IEnumerable<ComparisonResult2D> RankOrdered2D(IEnumerable<ComparisonResult2D> rows) =>
            rows.OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank);

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

            foreach (var result in RankOrdered(comparisonResults))
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
        //  2D single result
        // ───────────────────────────────────────────────────────

        public static void ExportSingleResult2DToCsv(
            string filename,
            ICuttingSolver2D solver,
            SolverResult2D result,
            SolverOptions2D options)
        {
            using var writer = new StreamWriter(filename, false, Encoding.UTF8);

            writer.WriteLine("2D 절단 최적화 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"알고리즘,{CsvEscape(solver.Name)}");
            writer.WriteLine($"시간 복잡도,{CsvEscape(solver.TimeComplexity)}");
            writer.WriteLine();

            writer.WriteLine("파라미터");
            writer.WriteLine($"Kerf,{options.Kerf}");
            writer.WriteLine($"Trim,{options.Trim}");
            writer.WriteLine($"AlphaArea (mm² 단가),{options.AlphaArea}");
            writer.WriteLine($"Stage,{options.Stage}");
            writer.WriteLine($"회전 허용,{options.AllowRotation}");
            writer.WriteLine($"시간 제한 (ms),{options.TimeLimitMs}");
            writer.WriteLine($"재고 사용 순서,{options.UsageOrder}");
            writer.WriteLine();

            writer.WriteLine("결과 요약");
            writer.WriteLine($"총 비용,{result.TotalCost}");
            writer.WriteLine($"낭비 면적 (mm²),{result.TotalWasteArea}");
            writer.WriteLine($"시트 사용,{result.SheetsUsed}");
            writer.WriteLine($"재료 효율 (%),{result.MaterialEfficiency:F2}");
            writer.WriteLine($"실행 시간 (ms),{result.ExecutionTimeMs:F3}");
            writer.WriteLine();

            writer.WriteLine("패턴 상세");
            writer.WriteLine("번호,시트 W,시트 H,수량,배치 수,효율(%)");
            int idx = 1;
            foreach (var p in result.Patterns)
            {
                writer.WriteLine($"{idx},{p.Sheet.Width},{p.Sheet.Height},{p.Multiplicity},{p.Placements.Count},{p.Efficiency:F1}");
                idx++;
            }
        }

        public static void ExportSingleResult2DToExcel(
            string filename,
            ICuttingSolver2D solver,
            SolverResult2D result,
            SolverOptions2D options)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("2D 최적화 결과");
            int row = 1;

            worksheet.Cell(row, 1).Value = "2D 절단 최적화 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 1).Value = "알고리즘:";
            worksheet.Cell(row++, 2).Value = solver.Name;
            row++;

            worksheet.Cell(row, 1).Value = "파라미터";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "Kerf:";    worksheet.Cell(row++, 2).Value = options.Kerf;
            worksheet.Cell(row, 1).Value = "Trim:";    worksheet.Cell(row++, 2).Value = options.Trim;
            worksheet.Cell(row, 1).Value = "AlphaArea:"; worksheet.Cell(row++, 2).Value = options.AlphaArea;
            worksheet.Cell(row, 1).Value = "Stage:";   worksheet.Cell(row++, 2).Value = options.Stage;
            worksheet.Cell(row, 1).Value = "회전 허용:"; worksheet.Cell(row++, 2).Value = options.AllowRotation;
            worksheet.Cell(row, 1).Value = "시간 제한:"; worksheet.Cell(row++, 2).Value = options.TimeLimitMs;
            worksheet.Cell(row, 1).Value = "재고 사용 순서:"; worksheet.Cell(row++, 2).Value = options.UsageOrder.ToString();
            row++;

            worksheet.Cell(row, 1).Value = "결과 요약";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "총 비용:";        worksheet.Cell(row++, 2).Value = result.TotalCost;
            worksheet.Cell(row, 1).Value = "낭비 면적 (mm²):"; worksheet.Cell(row++, 2).Value = result.TotalWasteArea;
            worksheet.Cell(row, 1).Value = "시트 사용:";       worksheet.Cell(row++, 2).Value = result.SheetsUsed;
            worksheet.Cell(row, 1).Value = "재료 효율 (%):";   worksheet.Cell(row++, 2).Value = result.MaterialEfficiency;
            worksheet.Cell(row, 1).Value = "실행 시간 (ms):";  worksheet.Cell(row++, 2).Value = result.ExecutionTimeMs;
            row++;

            worksheet.Cell(row, 1).Value = "패턴 상세";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "번호";
            worksheet.Cell(row, 2).Value = "시트 W";
            worksheet.Cell(row, 3).Value = "시트 H";
            worksheet.Cell(row, 4).Value = "수량";
            worksheet.Cell(row, 5).Value = "배치 수";
            worksheet.Cell(row, 6).Value = "효율(%)";
            worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
            row++;

            int idx = 1;
            foreach (var p in result.Patterns)
            {
                worksheet.Cell(row, 1).Value = idx;
                worksheet.Cell(row, 2).Value = p.Sheet.Width;
                worksheet.Cell(row, 3).Value = p.Sheet.Height;
                worksheet.Cell(row, 4).Value = p.Multiplicity;
                worksheet.Cell(row, 5).Value = p.Placements.Count;
                worksheet.Cell(row, 6).Value = p.Efficiency;
                row++;
                idx++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filename);
        }

        // ───────────────────────────────────────────────────────
        //  2D comparison
        // ───────────────────────────────────────────────────────

        public static void ExportComparison2DResultsToCsv(
            string filename,
            IEnumerable<ComparisonResult2D> rows)
        {
            using var writer = new StreamWriter(filename, false, Encoding.UTF8);

            writer.WriteLine("2D 알고리즘 비교 결과");
            writer.WriteLine($"날짜,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();

            writer.WriteLine("알고리즘,총 비용,낭비(mm²),시트 사용,효율(%),실행 시간(ms),순위");
            foreach (var r in RankOrdered2D(rows))
            {
                writer.WriteLine($"{CsvEscape(r.AlgorithmName)},{r.TotalCost},{r.WasteArea}," +
                                 $"{r.SheetsUsed},{r.MaterialEfficiency:F2},{r.ExecutionTimeMs:F3}," +
                                 $"{(r.Rank > 0 ? r.Rank.ToString() : "-")}");
            }
        }

        public static void ExportComparison2DResultsToExcel(
            string filename,
            IEnumerable<ComparisonResult2D> rows)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("2D 알고리즘 비교");
            int row = 1;

            worksheet.Cell(row, 1).Value = "2D 알고리즘 비교 결과";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "날짜:";
            worksheet.Cell(row++, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            row++;

            worksheet.Cell(row, 1).Value = "알고리즘";
            worksheet.Cell(row, 2).Value = "총 비용";
            worksheet.Cell(row, 3).Value = "낭비(mm²)";
            worksheet.Cell(row, 4).Value = "시트 사용";
            worksheet.Cell(row, 5).Value = "효율(%)";
            worksheet.Cell(row, 6).Value = "실행 시간(ms)";
            worksheet.Cell(row, 7).Value = "순위";
            worksheet.Range(row, 1, row, 7).Style.Font.Bold = true;
            row++;

            foreach (var r in RankOrdered2D(rows))
            {
                worksheet.Cell(row, 1).Value = r.AlgorithmName;
                worksheet.Cell(row, 2).Value = r.TotalCost;
                worksheet.Cell(row, 3).Value = r.WasteArea;
                worksheet.Cell(row, 4).Value = r.SheetsUsed;
                worksheet.Cell(row, 5).Value = r.MaterialEfficiency;
                worksheet.Cell(row, 6).Value = r.ExecutionTimeMs;
                worksheet.Cell(row, 7).Value = r.Rank > 0 ? r.Rank.ToString() : "-";
                if (r.Rank == 1)
                    worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightGreen;
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
