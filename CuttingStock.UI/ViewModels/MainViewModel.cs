using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CuttingStock.Core.Algorithms;
using CuttingStock.Core.Domain;
using CuttingStock.Core.Models;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// MVVM ViewModel for the 1D Rebar tab. Owns user input state, parameter
    /// strings, command implementations, and the result/visualization output
    /// the View binds to. The View remains responsible for:
    ///   - DataGrid selection (forwarded via DeleteSelected{Stock,Order}),
    ///   - rendering the visualization rows into the ItemsControl,
    ///   - updating LiveCharts series (called after Compare completes).
    /// </summary>
    public sealed partial class MainViewModel : SolverWorkspaceViewModel
    {
        private readonly IDialogService _dialog;

        public MainViewModel(IDialogService dialog)
        {
            _dialog = dialog;
            Stocks = new ObservableCollection<StockRow>();
            Orders = new ObservableCollection<OrderRow>();
            ComparisonResults = new ObservableCollection<ComparisonResult>();
        }

        public IReadOnlyList<SolverDescriptor> SolverDescriptors => SolverCatalog.All;
        public SolverDescriptor SelectedSolverDescriptor => SolverCatalog.GetByIndex(AlgorithmIndex);
        public string SelectedSolverDescription => SelectedSolverDescriptor.Description;
        public string SelectedSolverTimeComplexity => SelectedSolverDescriptor.TimeComplexity;
        public string SelectedSolverCapabilityText => SelectedSolverDescriptor.CapabilitySummary;
        public string SelectedSolverAdvancedNotes => SelectedSolverDescriptor.AdvancedNotes;
        public bool CanConfigureWelding => SelectedSolverDescriptor.Supports(SolverCapability.Welding);
        public string WeldingOptionTip => CanConfigureWelding
            ? "체크하면 재고 길이를 초과하는 주문을 Delta 이상의 조각들로 분할해 용접"
            : $"{SelectedSolverDescriptor.Name}은(는) 용접 옵션을 지원하지 않습니다.";

        public ObservableCollection<StockRow> Stocks { get; }
        public ObservableCollection<OrderRow> Orders { get; }
        public ObservableCollection<ComparisonResult> ComparisonResults { get; }

        [ObservableProperty] private string _alphaText = "1.0";
        [ObservableProperty] private string _betaText = "500";
        [ObservableProperty] private string _gammaText = "100";
        [ObservableProperty] private string _deltaText = "100";
        [ObservableProperty] private string _kerfText = "0";

        /// <summary>0 = SmallToLarge, 1 = LargeToSmall (matches ComboBox order).</summary>
        [ObservableProperty] private int _usageOrderIndex;

        /// <summary>0 = Greedy, 1 = CG, 2 = ArcFlow (matches ComboBox order).</summary>
        [ObservableProperty] private int _algorithmIndex;

        [ObservableProperty] private bool _enableWelding;

        [ObservableProperty] private string _resultText = string.Empty;
        [ObservableProperty] private string _comparisonText = string.Empty;
        [ObservableProperty] private string _statusTip = "Ctrl+R 실행 · Ctrl+Shift+C 비교 · F1 예제 · Esc 취소";
        [ObservableProperty] private bool _hasSingleResult;
        [ObservableProperty] private bool _hasComparisonResults;
        [ObservableProperty] private List<VisualizationRow>? _visualizationRows;
        [ObservableProperty] private List<LegendItem>? _legendItems;

        private SolverResult? _lastResult;
        private SolverOptions? _lastOptions;
        private ICuttingSolver? _lastSolver;

        public event EventHandler<string>? ScenarioSaved;
        public event EventHandler<string>? ScenarioLoaded;
    }
}
