using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CuttingStock.Core.Domain;
using CuttingStock.Core.TwoD.Algorithms;
using CuttingStock.Core.TwoD.Domain;
using CuttingStock.Core.TwoD.Models;
using CuttingStock.UI.Services;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// MVVM ViewModel for the 2D Sheet tab. Owns user input collections,
    /// parameter strings, command implementations, and the result/comparison
    /// state the View binds to. The View remains responsible for:
    ///   - DataGrid selection (forwarded via DeleteSelected{Sheets,Orders}),
    ///   - rendering the placement Canvas patterns,
    ///   - updating LiveCharts series after Compare completes,
    /// because those touch WPF visual types directly.
    /// </summary>
    public sealed partial class TwoDViewModel : SolverWorkspaceViewModel
    {
        private readonly IDialogService _dialog;

        public TwoDViewModel(IDialogService dialog)
        {
            _dialog = dialog;
            Sheets = new ObservableCollection<SheetRow>();
            Orders = new ObservableCollection<RectOrderRow>();
            CompareRows = new ObservableCollection<ComparisonResult2D>();
        }

        public IReadOnlyList<SolverDescriptor2D> SolverDescriptors => SolverCatalog2D.All;
        public SolverDescriptor2D SelectedSolverDescriptor => SolverCatalog2D.GetByIndex(AlgorithmIndex);
        public string SelectedSolverDescription => SelectedSolverDescriptor.Description;
        public string SelectedSolverCapabilityText => SelectedSolverDescriptor.CapabilitySummary;
        public string SelectedSolverAdvancedNotes => SelectedSolverDescriptor.AdvancedNotes;
        public bool CanConfigureTimeLimit => SelectedSolverDescriptor.Supports(SolverCapability.TimeLimit);
        public bool CanConfigureStage =>
            SelectedSolverDescriptor.Supports(SolverCapability.EnforcedStage) &&
            SelectedSolverDescriptor.SupportedStages.Count > 1;
        public string TimeLimitOptionTip => CanConfigureTimeLimit
            ? "CG/MIP 솔버 wall-clock 제한 (ms)"
            : $"{SelectedSolverDescriptor.Name}은(는) 시간 제한을 사용하지 않습니다.";
        public string StageOptionTip => CanConfigureStage
            ? "선택한 stage 수를 절단 패턴 제약으로 강제합니다."
            : SelectedSolverDescriptor.Supports(SolverCapability.EnforcedStage)
                ? $"{SelectedSolverDescriptor.Name}은(는) {string.Join("/", SelectedSolverDescriptor.SupportedStages)}-stage로 고정됩니다."
                : "현재 선택한 solver는 Stage 값을 강제하지 않습니다. 패턴은 unrestricted guillotine입니다.";

        public ObservableCollection<SheetRow> Sheets { get; }
        public ObservableCollection<RectOrderRow> Orders { get; }
        public ObservableCollection<ComparisonResult2D> CompareRows { get; }

        [ObservableProperty] private string _kerfText = "0";
        [ObservableProperty] private string _trimText = "0";
        [ObservableProperty] private string _alphaAreaText = "1";
        [ObservableProperty] private string _timeLimitText = "30000";
        [ObservableProperty] private bool _allowRotation = true;
        /// <summary>0 = 2-stage, 1 = 3-stage.</summary>
        [ObservableProperty] private int _stageIndex;
        /// <summary>0 = Small→Large, 1 = Large→Small. Default Large→Small (index 1).</summary>
        [ObservableProperty] private int _usageOrderIndex = 1;
        /// <summary>0 = Shelf, 1 = CG2D, 2 = StagedMip.</summary>
        [ObservableProperty] private int _algorithmIndex;

        [ObservableProperty] private string _reportText = string.Empty;
        [ObservableProperty] private string _compareText = string.Empty;
        [ObservableProperty] private bool _hasSingleResult;
        [ObservableProperty] private bool _hasComparisonResults;
        [ObservableProperty] private TwoDRenderProjection? _renderProjection;
        [ObservableProperty] private TwoDChartProjection _chartProjection = TwoDChartProjection.Empty;

        private SolverResult2D? _lastResult;
        private SolverOptions2D? _lastOptions;
        private ICuttingSolver2D? _lastSolver;

        public SolverResult2D? LastResult => _lastResult;
        public SolverOptions2D? LastOptions => _lastOptions;
    }
}
