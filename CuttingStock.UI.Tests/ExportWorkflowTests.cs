using System;
using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class ExportWorkflowTests
    {
        private static readonly ExportDialogRequest Request = new(
            "CSV 저장",
            "CSV 파일 (*.csv)|*.csv",
            "result.csv",
            "CSV 파일로 저장되었습니다.",
            "먼저 최적화를 실행해주세요.");

        [Test]
        public void TryExport_NoDataShowsWarningWithoutPrompting()
        {
            var dialog = new FakeDialogService();
            bool exported = false;

            bool result = ExportWorkflow.TryExport(
                dialog,
                hasData: false,
                Request,
                _ => exported = true);

            result.Should().BeFalse();
            exported.Should().BeFalse();
            dialog.Messages.Should().ContainSingle(message =>
                message.Severity == "warning" && message.Title == "내보내기 불가");
        }

        [Test]
        public void TryExport_CancelledPromptDoesNotInvokeExporter()
        {
            var dialog = new FakeDialogService();
            dialog.SavePathResponses.Enqueue(null);
            bool exported = false;

            bool result = ExportWorkflow.TryExport(
                dialog,
                hasData: true,
                Request,
                _ => exported = true);

            result.Should().BeFalse();
            exported.Should().BeFalse();
            dialog.Messages.Should().ContainSingle(message => message.Severity == "save");
        }

        [Test]
        public void TryExport_SuccessInvokesExporterAndReportsPath()
        {
            var dialog = new FakeDialogService();
            dialog.SavePathResponses.Enqueue("output.csv");
            string? exportedPath = null;

            bool result = ExportWorkflow.TryExport(
                dialog,
                hasData: true,
                Request,
                path => exportedPath = path);

            result.Should().BeTrue();
            exportedPath.Should().Be("output.csv");
            dialog.Messages.Should().Contain(message =>
                message.Severity == "info" && message.Message.Contains("output.csv"));
        }

        [Test]
        public void TryExport_ExporterThrowsReportsError()
        {
            var dialog = new FakeDialogService();
            dialog.SavePathResponses.Enqueue("output.csv");

            bool result = ExportWorkflow.TryExport(
                dialog,
                hasData: true,
                Request,
                _ => throw new InvalidOperationException("disk full"));

            result.Should().BeFalse();
            dialog.Messages.Should().Contain(message =>
                message.Severity == "error" && message.Message.Contains("disk full"));
        }
    }
}
