using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;

namespace CuttingStock.UI.Services
{
    public static partial class ExportService
    {
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
            worksheet.Cell(row, 1).Value = "Kerf:"; worksheet.Cell(row++, 2).Value = options.Kerf;
            worksheet.Cell(row, 1).Value = "Trim:"; worksheet.Cell(row++, 2).Value = options.Trim;
            worksheet.Cell(row, 1).Value = "AlphaArea:"; worksheet.Cell(row++, 2).Value = options.AlphaArea;
            worksheet.Cell(row, 1).Value = "Stage:"; worksheet.Cell(row++, 2).Value = options.Stage;
            worksheet.Cell(row, 1).Value = "회전 허용:"; worksheet.Cell(row++, 2).Value = options.AllowRotation;
            worksheet.Cell(row, 1).Value = "시간 제한:"; worksheet.Cell(row++, 2).Value = options.TimeLimitMs;
            worksheet.Cell(row, 1).Value = "재고 사용 순서:"; worksheet.Cell(row++, 2).Value = options.UsageOrder.ToString();
            row++;

            worksheet.Cell(row, 1).Value = "결과 요약";
            worksheet.Cell(row++, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 1).Value = "총 비용:"; worksheet.Cell(row++, 2).Value = result.TotalCost;
            worksheet.Cell(row, 1).Value = "낭비 면적 (mm²):"; worksheet.Cell(row++, 2).Value = result.TotalWasteArea;
            worksheet.Cell(row, 1).Value = "시트 사용:"; worksheet.Cell(row++, 2).Value = result.SheetsUsed;
            worksheet.Cell(row, 1).Value = "재료 효율 (%):"; worksheet.Cell(row++, 2).Value = result.MaterialEfficiency;
            worksheet.Cell(row, 1).Value = "실행 시간 (ms):"; worksheet.Cell(row++, 2).Value = result.ExecutionTimeMs;
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

        private static IEnumerable<ComparisonResult2D> RankOrdered2D(IEnumerable<ComparisonResult2D> rows) =>
            rows.OrderBy(r => r.Rank == 0 ? int.MaxValue : r.Rank);
    }
}
