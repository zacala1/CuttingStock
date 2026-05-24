using System.Collections.Generic;
using System.Windows.Media;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>One stock-bar row in the 1D visualization tab.</summary>
    public sealed class VisualizationRow
    {
        public required string InfoText { get; init; }
        public List<VisualizationBlock> Blocks { get; init; } = new();
    }

    /// <summary>One coloured rectangle inside a <see cref="VisualizationRow"/>.</summary>
    public sealed class VisualizationBlock
    {
        public double Width { get; init; }
        public required Brush Color { get; init; }
        public required Brush BorderColor { get; init; }
        public required string ToolTip { get; init; }
        public required string Text { get; init; }
        public required Brush TextColor { get; init; }
    }

    /// <summary>Legend chip pairing a colour with a label.</summary>
    public sealed class LegendItem
    {
        public required Brush Color { get; init; }
        public required string Label { get; init; }
    }
}
