using System.IO;
using ClosedXML.Excel;
using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class ScenarioImportServiceTests
    {
        private string _tempDirectory = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDirectory = Path.Combine(
                Path.GetTempPath(),
                $"CuttingStockImportTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }

        [Test]
        public void ReadLengthQuantityRows_CsvWithHeader_ReturnsOnlyValidRows()
        {
            string path = Path.Combine(_tempDirectory, "rows.csv");
            File.WriteAllText(
                path,
                "Length,Quantity\n12000,2\nbad,3\n6000,4\n5000,-1");

            var rows = ScenarioImportService.ReadLengthQuantityRows(path);

            rows.Should().Equal(
                new LengthQuantityInput(12000, 2),
                new LengthQuantityInput(6000, 4));
        }

        [Test]
        public void ReadLengthQuantityRows_CsvWithoutHeader_KeepsFirstRow()
        {
            string path = Path.Combine(_tempDirectory, "rows.csv");
            File.WriteAllText(path, "12000,2\n6000,4");

            ScenarioImportService.ReadLengthQuantityRows(path).Should().Equal(
                new LengthQuantityInput(12000, 2),
                new LengthQuantityInput(6000, 4));
        }

        [Test]
        public void ReadLengthQuantityRows_XlsxUsesFirstWorksheetAndHeaderDetection()
        {
            string path = Path.Combine(_tempDirectory, "rows.xlsx");
            using (var workbook = new XLWorkbook())
            {
                var first = workbook.AddWorksheet("Input");
                first.Cell(1, 1).Value = "Length";
                first.Cell(1, 2).Value = "Quantity";
                first.Cell(2, 1).Value = 12000;
                first.Cell(2, 2).Value = 2;
                first.Cell(3, 1).Value = "invalid";
                first.Cell(3, 2).Value = 3;
                workbook.AddWorksheet("Ignored").Cell(1, 1).Value = 999;
                workbook.SaveAs(path);
            }

            ScenarioImportService.ReadLengthQuantityRows(path).Should().Equal(
                new LengthQuantityInput(12000, 2));
        }

        [Test]
        public void ReadLengthQuantityRows_UnsupportedExtension_ReturnsEmpty()
        {
            string path = Path.Combine(_tempDirectory, "rows.txt");
            File.WriteAllText(path, "12000,2");

            ScenarioImportService.ReadLengthQuantityRows(path).Should().BeEmpty();
        }

        [Test]
        public void ReadLengthQuantityRows_MissingFile_PropagatesToViewErrorBoundary()
        {
            string path = Path.Combine(_tempDirectory, "missing.csv");

            Action read = () => ScenarioImportService.ReadLengthQuantityRows(path);

            read.Should().Throw<FileNotFoundException>();
        }

        [Test]
        public void ReadLengthQuantityRows_CorruptWorkbook_PropagatesToViewErrorBoundary()
        {
            string path = Path.Combine(_tempDirectory, "corrupt.xlsx");
            File.WriteAllText(path, "not an xlsx package");

            Action read = () => ScenarioImportService.ReadLengthQuantityRows(path);

            read.Should().Throw<Exception>();
        }

        [Test]
        public void ReadLengthQuantityRows_EmptyWorksheet_ReturnsEmpty()
        {
            string path = Path.Combine(_tempDirectory, "empty.xlsx");
            using (var workbook = new XLWorkbook())
            {
                workbook.AddWorksheet("Empty");
                workbook.SaveAs(path);
            }

            ScenarioImportService.ReadLengthQuantityRows(path).Should().BeEmpty();
        }
    }
}
