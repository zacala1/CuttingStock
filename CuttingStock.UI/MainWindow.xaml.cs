using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CuttingStock.Core.Models;
using CuttingStock.Core.Persistence;
using CuttingStock.UI.Services;
using CuttingStock.UI.ViewModels;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace CuttingStock
{
    /// <summary>
    /// 1D 메인 윈도우. MVVM 전환 후 thin view 역할만 한다:
    ///   - DataContext에 MainViewModel을 바인딩.
    ///   - DataGrid SelectedItems / 파일 다이얼로그 / 클립보드 / 키 단축키 등
    ///     ViewModel이 직접 처리하기 어려운 WPF 정합성 코드만 남김.
    ///   - LiveCharts 시리즈는 코드에서 직접 갱신하고, ComparisonResults가
    ///     채워질 때 ViewModel.PropertyChanged 이벤트로 트리거됨.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly WorkspaceShortcutTarget _oneDShortcutTarget;
        private UserPreferences _prefs = new();

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel(new DialogService());
            _oneDShortcutTarget = new WorkspaceShortcutTarget(
                _vm.LoadExampleCommand,
                _vm.CalculateCommand,
                _vm.CompareAlgorithmsCommand,
                _vm.ExportToExcelCommand,
                _vm.CancelCommand,
                () => _vm.HasSingleResult);
            DataContext = _vm;
            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.ScenarioSaved += (_, path) => OnScenarioPathUsed(_prefs.Recent1D, path);
            _vm.ScenarioLoaded += (_, path) => OnScenarioPathUsed(_prefs.Recent1D, path);
            twoDTab.ScenarioSaved += (_, path) => OnScenarioPathUsed(_prefs.Recent2D, path);
            twoDTab.ScenarioLoaded += (_, path) => OnScenarioPathUsed(_prefs.Recent2D, path);
            twoDTab.RecentScenariosRequested += (_, _) => ShowRecent2D();
        }

        /// <summary>Push a recently-touched scenario path into the MRU list and persist.</summary>
        private void OnScenarioPathUsed(List<string> recent, string path)
        {
            RecentScenarioService.Touch(recent, path);
            UserPreferencesStore.Save(_prefs);
        }

        // ─── Preferences: window state, last tab, last algorithm ──────

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _prefs = UserPreferencesStore.Load();

            if (_prefs.WindowMaximized)
            {
                WindowState = WindowState.Maximized;
            }
            else
            {
                if (_prefs.WindowWidth  > 200) Width  = _prefs.WindowWidth;
                if (_prefs.WindowHeight > 200) Height = _prefs.WindowHeight;
                if (_prefs.WindowLeft.HasValue && _prefs.WindowTop.HasValue)
                {
                    Left = _prefs.WindowLeft.Value;
                    Top  = _prefs.WindowTop.Value;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                }
            }

            topTabControl.SelectedIndex = Math.Clamp(_prefs.LastTopTabIndex, 0, 1);
            _vm.AlgorithmIndex = Math.Clamp(_prefs.LastAlgorithm1D, 0, _vm.SolverDescriptors.Count - 1);
            twoDTab.AlgorithmIndex = Math.Clamp(_prefs.LastAlgorithm2D, 0, twoDTab.SolverCount - 1);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _prefs.WindowMaximized = WindowState == WindowState.Maximized;
            if (!_prefs.WindowMaximized)
            {
                _prefs.WindowWidth  = Width;
                _prefs.WindowHeight = Height;
                _prefs.WindowLeft   = Left;
                _prefs.WindowTop    = Top;
            }
            _prefs.LastTopTabIndex = topTabControl.SelectedIndex;
            _prefs.LastAlgorithm1D = _vm.AlgorithmIndex;
            _prefs.LastAlgorithm2D = twoDTab.AlgorithmIndex;
            UserPreferencesStore.Save(_prefs);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _vm.Dispose();
            twoDTab.Dispose();
        }

        // ─── Recent scenarios dropdown ────────────────────────────────

        private void Recent1D_Click(object sender, RoutedEventArgs e)
        {
            ShowRecentMenu(
                btnRecent1D,
                _prefs.Recent1D,
                _vm.LoadScenarioFromPath);
        }

        private void ShowRecent2D()
        {
            ShowRecentMenu(
                twoDTab.RecentMenuPlacementTarget,
                _prefs.Recent2D,
                twoDTab.LoadScenarioFromPath);
        }

        private void ShowRecentMenu(
            FrameworkElement placementTarget,
            List<string> recent,
            Func<string, bool> load)
        {
            var menu = new ContextMenu
            {
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = placementTarget,
            };
            if (recent.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "(최근 시나리오 없음)", IsEnabled = false });
            }
            else
            {
                foreach (var entry in RecentScenarioService.BuildEntries(recent))
                {
                    var menuItem = new MenuItem
                    {
                        Header = entry.Exists
                            ? entry.DisplayName
                            : $"{entry.DisplayName} (파일 없음)",
                        ToolTip = entry.Path,
                    };
                    var capturedPath = entry.Path;
                    menuItem.Click += (_, _) =>
                        LoadRecentScenario(recent, capturedPath, load);
                    menu.Items.Add(menuItem);
                }
                menu.Items.Add(new Separator());
                var clear = new MenuItem { Header = "목록 지우기" };
                clear.Click += (_, _) =>
                {
                    RecentScenarioService.Clear(recent);
                    UserPreferencesStore.Save(_prefs);
                };
                menu.Items.Add(clear);
            }
            menu.IsOpen = true;
        }

        private void LoadRecentScenario(
            List<string> recent,
            string path,
            Func<string, bool> load)
        {
            if (!File.Exists(path))
            {
                // Ask the user before silently rewriting the MRU — they may have
                // moved the file rather than deleted it and want to fix the path
                // themselves before we forget about it.
                var choice = MessageBox.Show(
                    $"파일을 찾을 수 없습니다:\n{path}\n\n최근 목록에서 제거하시겠습니까?",
                    "파일 없음", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Yes)
                {
                    RecentScenarioService.Remove(recent, path);
                    UserPreferencesStore.Save(_prefs);
                }
                return;
            }

            load(path);
        }

        // ─── Drag-and-drop scenario load ──────────────────────────────

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files is { Length: 1 } &&
                    ScenarioFileRouteService.IsCandidate(files[0]))
                {
                    e.Effects = DragDropEffects.Copy;
                }
            }
            e.Handled = true;
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;
            if (files.Length > 1)
            {
                MessageBox.Show($"한 번에 하나의 시나리오 파일만 열 수 있습니다. {files.Length}개 파일이 드롭됨.",
                    "여러 파일", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var path = files[0];

            try
            {
                switch (ScenarioFileRouteService.DetectRoute(path))
                {
                    case ScenarioRoute.OneD:
                        topTabControl.SelectedIndex = 0;
                        _vm.LoadScenarioFromPath(path);
                        break;
                    case ScenarioRoute.TwoD:
                        topTabControl.SelectedIndex = 1;
                        twoDTab.LoadScenarioFromPath(path);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"드롭한 파일을 불러올 수 없습니다.\n{ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // LiveCharts series can't be built declaratively in XAML (the
            // ColumnSeries<double> generic needs WPF runtime types), so we
            // refresh them whenever the comparison results change.
            if (e.PropertyName == nameof(MainViewModel.HasComparisonResults) && _vm.HasComparisonResults)
                UpdateCharts();
        }

        // ─── DataGrid: selection / paste / cell validation ───────────

        private void DeleteSelectedStock_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedStocks(stockGrid.SelectedItems.Cast<StockRow>());

        private void DeleteSelectedOrder_Click(object sender, RoutedEventArgs e) =>
            _vm.DeleteSelectedOrders(orderGrid.SelectedItems.Cast<OrderRow>());

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Cancel) return;
            if (e.EditingElement is TextBox tb && (!int.TryParse(tb.Text, out int v) || v <= 0))
            {
                MessageBox.Show("양의 정수를 입력해주세요.", "입력 오류",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
        }

        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            if (sender is not DataGrid grid) return;

            try
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                var rows = ClipboardRowParser.ParseLengthQuantityRows(text);
                if (grid.ItemsSource == _vm.Stocks)
                {
                    foreach (var row in rows)
                        _vm.Stocks.Add(new StockRow { Length = row.Length, Quantity = row.Quantity });
                }
                else if (grid.ItemsSource == _vm.Orders)
                {
                    foreach (var row in rows)
                        _vm.Orders.Add(new OrderRow { Length = row.Length, Quantity = row.Quantity });
                }
                else return;

                int added = rows.Count;
                if (added > 0)
                    MessageBox.Show($"{added}개의 항목을 붙여넣었습니다.", "붙여넣기 성공",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"붙여넣기 중 오류가 발생했습니다: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── File import (CSV / XLSX → ObservableCollection) ─────────

        private void ImportStock_Click(object sender, RoutedEventArgs e)
        {
            int added = ImportFromFile(
                addStock:  (l, q) => _vm.Stocks.Add(new StockRow { Length = l, Quantity = q }),
                addOrder:  null);
            if (added >= 0)
                MessageBox.Show($"{added}개의 데이터를 불러왔습니다.", "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportOrder_Click(object sender, RoutedEventArgs e)
        {
            int added = ImportFromFile(
                addStock: null,
                addOrder: (l, q) => _vm.Orders.Add(new OrderRow { Length = l, Quantity = q }));
            if (added >= 0)
                MessageBox.Show($"{added}개의 데이터를 불러왔습니다.", "완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private int ImportFromFile(Action<int, int>? addStock, Action<int, int>? addOrder)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel/CSV 파일 (*.xlsx;*.csv)|*.xlsx;*.csv|모든 파일 (*.*)|*.*",
                Title = "데이터 불러오기",
            };
            if (dlg.ShowDialog() != true) return -1;

            try
            {
                var rows = ScenarioImportService.ReadLengthQuantityRows(dlg.FileName);
                foreach (var row in rows)
                {
                    addStock?.Invoke(row.Length, row.Quantity);
                    addOrder?.Invoke(row.Length, row.Quantity);
                }

                return rows.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 읽기 실패: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return -1;
            }
        }

        // ─── Numeric input filtering ─────────────────────────────────

        private static readonly Regex IntegerRegex = new("[^0-9]+", RegexOptions.Compiled);
        private static readonly Regex DecimalRegex = new("[^0-9.]+", RegexOptions.Compiled);

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
            e.Handled = IntegerRegex.IsMatch(e.Text);

        private void DecimalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DecimalRegex.IsMatch(e.Text)) { e.Handled = true; return; }
            if (e.Text == "." && sender is TextBox tb && tb.Text.Contains('.')) e.Handled = true;
        }

        // ─── Keyboard shortcuts ──────────────────────────────────────

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl  = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift)   == ModifierKeys.Shift;

            if (e.Key == Key.F1)
            { e.Handled = ExecuteWorkspaceShortcut(WorkspaceShortcut.LoadExample); return; }
            if (ctrl && !shift && e.Key == Key.R)
            { e.Handled = ExecuteWorkspaceShortcut(WorkspaceShortcut.Calculate); return; }
            if (ctrl && shift && e.Key == Key.C)
            { e.Handled = ExecuteWorkspaceShortcut(WorkspaceShortcut.Compare); return; }
            if (ctrl && !shift && e.Key == Key.S)
            { e.Handled = ExecuteWorkspaceShortcut(WorkspaceShortcut.ExportToExcel); return; }
            if (ctrl && !shift && e.Key == Key.F && topTabControl.SelectedIndex == 0)
            {
                // Ctrl+F should open the search bar OR re-focus the box if it's
                // already open — never close it. Closing is Esc's job.
                if (searchBar.Visibility == Visibility.Visible)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                }
                else
                {
                    ToggleSearchBar(true);
                }
                e.Handled = true; return;
            }
            if (e.Key == Key.F3 && shift && topTabControl.SelectedIndex == 0)
            { SearchPrev_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (e.Key == Key.F3 && topTabControl.SelectedIndex == 0)
            { SearchNext_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            if (e.Key == Key.Escape &&
                ExecuteWorkspaceShortcut(WorkspaceShortcut.Cancel))
            { e.Handled = true; return; }
            if (e.Key == Key.Escape && searchBar.Visibility == Visibility.Visible)
            { ToggleSearchBar(false); e.Handled = true; }
        }

        private bool ExecuteWorkspaceShortcut(WorkspaceShortcut shortcut) =>
            WorkspaceShortcutDispatcher.TryExecute(
                topTabControl.SelectedIndex,
                shortcut,
                _oneDShortcutTarget,
                twoDTab.ShortcutTarget);

        // ─── Search bar (Ctrl+F) ─────────────────────────────────────

        /// <summary>Close the search bar when leaving the 1D tab (it's anchored inside 1D).</summary>
        private void TopTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OriginalSource, topTabControl)) return;
            if (topTabControl.SelectedIndex != 0 && searchBar != null && searchBar.Visibility == Visibility.Visible)
                ToggleSearchBar(false);
        }

        private void ToggleSearchBar(bool show)
        {
            searchBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show) { searchBox.Focus(); searchBox.SelectAll(); }
            else { searchStatus.Text = string.Empty; }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            { SearchNext_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Escape)
            { ToggleSearchBar(false); e.Handled = true; }
        }

        private void SearchNext_Click(object sender, RoutedEventArgs e) => FindAndSelect(forward: true);
        private void SearchPrev_Click(object sender, RoutedEventArgs e) => FindAndSelect(forward: false);
        private void SearchClose_Click(object sender, RoutedEventArgs e) => ToggleSearchBar(false);

        private void FindAndSelect(bool forward)
        {
            string needle = searchBox.Text;
            if (string.IsNullOrEmpty(needle)) { searchStatus.Text = string.Empty; return; }
            string haystack = resultTextBox.Text ?? string.Empty;
            if (haystack.Length == 0) { searchStatus.Text = "결과 없음"; return; }

            TextSearchMatch match = TextSearchService.Find(
                haystack,
                needle,
                resultTextBox.SelectionStart,
                resultTextBox.SelectionLength,
                forward);
            if (!match.Found) { searchStatus.Text = "찾을 수 없음"; return; }

            resultTextBox.Focus();
            resultTextBox.Select(match.Index, match.Length);
            resultTextBox.ScrollToLine(resultTextBox.GetLineIndexFromCharacterIndex(match.Index));
            searchStatus.Text = $"{match.Index + 1} 위치";
        }

        // ─── LiveCharts series refresh (called from PropertyChanged) ─

        private void UpdateCharts()
        {
            var successResults = _vm.ComparisonResults.Where(r => r.Success).ToList();
            if (successResults.Count == 0) return;

            string[] labels = successResults.Select(r => AbbreviateName(r.AlgorithmName)).ToArray();

            costChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => (double)r.TotalCost).ToArray(),
                    Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                },
            };
            costChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            costChart.YAxes = new[] { new Axis { Name = "비용 (원)" } };

            efficiencyChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.MaterialEfficiency).ToArray(),
                    Fill = new SolidColorPaint(SKColors.MediumSeaGreen),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}%",
                },
            };
            efficiencyChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            efficiencyChart.YAxes = new[] { new Axis { Name = "효율 (%)", MinLimit = 0, MaxLimit = 100 } };

            timeChart.Series = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Values = successResults.Select(r => r.ExecutionTimeMs).ToArray(),
                    Fill = new SolidColorPaint(SKColors.Coral),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = pt => $"{pt.Coordinate.PrimaryValue:F1}ms",
                },
            };
            timeChart.XAxes = new[] { new Axis { Labels = labels, LabelsRotation = -15 } };
            timeChart.YAxes = new[] { new Axis { Name = "시간 (ms)" } };
        }

        private static string AbbreviateName(string name)
        {
            int paren = name.IndexOf('(');
            if (paren > 0 && paren < name.Length - 1)
                return name[..paren].TrimEnd() + Environment.NewLine + name[paren..];
            return name;
        }
    }
}
