using System;
using System.Windows.Input;
using CuttingStock.UI.Services;
using FluentAssertions;
using NUnit.Framework;

namespace CuttingStock.UI.Tests
{
    [TestFixture]
    public class WorkspaceShortcutDispatcherTests
    {
        [TestCase(WorkspaceShortcut.LoadExample)]
        [TestCase(WorkspaceShortcut.Calculate)]
        [TestCase(WorkspaceShortcut.Compare)]
        [TestCase(WorkspaceShortcut.ExportToExcel)]
        [TestCase(WorkspaceShortcut.Cancel)]
        public void TryExecute_WhenTwoDTabIsSelected_DispatchesOnlyToTwoD(
            WorkspaceShortcut shortcut)
        {
            var oneD = CreateTarget();
            var twoD = CreateTarget();

            var handled = WorkspaceShortcutDispatcher.TryExecute(
                selectedWorkspaceIndex: 1,
                shortcut,
                oneD.Target,
                twoD.Target);

            handled.Should().BeTrue();
            oneD.ExecutionCount(shortcut).Should().Be(0);
            twoD.ExecutionCount(shortcut).Should().Be(1);
        }

        [Test]
        public void TryExecute_WhenOneDTabIsSelected_DispatchesOnlyToOneD()
        {
            var oneD = CreateTarget();
            var twoD = CreateTarget();

            var handled = WorkspaceShortcutDispatcher.TryExecute(
                selectedWorkspaceIndex: 0,
                WorkspaceShortcut.Calculate,
                oneD.Target,
                twoD.Target);

            handled.Should().BeTrue();
            oneD.ExecutionCount(WorkspaceShortcut.Calculate).Should().Be(1);
            twoD.ExecutionCount(WorkspaceShortcut.Calculate).Should().Be(0);
        }

        [Test]
        public void TryExecute_WhenExportHasNoResult_DoesNotExecuteCommand()
        {
            var oneD = CreateTarget(canExport: false);
            var twoD = CreateTarget();

            var handled = WorkspaceShortcutDispatcher.TryExecute(
                selectedWorkspaceIndex: 0,
                WorkspaceShortcut.ExportToExcel,
                oneD.Target,
                twoD.Target);

            handled.Should().BeFalse();
            oneD.ExecutionCount(WorkspaceShortcut.ExportToExcel).Should().Be(0);
        }

        [Test]
        public void TryExecute_WhenCommandCannotExecute_DoesNotHandleOrExecute()
        {
            var oneD = CreateTarget(canExecute: false);
            var twoD = CreateTarget();

            var handled = WorkspaceShortcutDispatcher.TryExecute(
                selectedWorkspaceIndex: 0,
                WorkspaceShortcut.Cancel,
                oneD.Target,
                twoD.Target);

            handled.Should().BeFalse();
            oneD.ExecutionCount(WorkspaceShortcut.Cancel).Should().Be(0);
        }

        private static TargetFixture CreateTarget(
            bool canExport = true,
            bool canExecute = true)
        {
            var loadExample = new RecordingCommand(canExecute);
            var calculate = new RecordingCommand(canExecute);
            var compare = new RecordingCommand(canExecute);
            var export = new RecordingCommand(canExecute);
            var cancel = new RecordingCommand(canExecute);

            return new TargetFixture(
                new WorkspaceShortcutTarget(
                    loadExample,
                    calculate,
                    compare,
                    export,
                    cancel,
                    () => canExport),
                loadExample,
                calculate,
                compare,
                export,
                cancel);
        }

        private sealed record TargetFixture(
            WorkspaceShortcutTarget Target,
            RecordingCommand LoadExample,
            RecordingCommand Calculate,
            RecordingCommand Compare,
            RecordingCommand Export,
            RecordingCommand Cancel)
        {
            public int ExecutionCount(WorkspaceShortcut shortcut) => shortcut switch
            {
                WorkspaceShortcut.LoadExample => LoadExample.ExecutionCount,
                WorkspaceShortcut.Calculate => Calculate.ExecutionCount,
                WorkspaceShortcut.Compare => Compare.ExecutionCount,
                WorkspaceShortcut.ExportToExcel => Export.ExecutionCount,
                WorkspaceShortcut.Cancel => Cancel.ExecutionCount,
                _ => throw new ArgumentOutOfRangeException(nameof(shortcut)),
            };
        }

        private sealed class RecordingCommand : ICommand
        {
            private readonly bool _canExecute;

            public RecordingCommand(bool canExecute) => _canExecute = canExecute;

            public int ExecutionCount { get; private set; }

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => _canExecute;

            public void Execute(object? parameter) => ExecutionCount++;
        }
    }
}
