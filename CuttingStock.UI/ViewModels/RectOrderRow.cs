using CommunityToolkit.Mvvm.ComponentModel;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// Mutable row type for the 2D order DataGrid.
    /// </summary>
    public sealed partial class RectOrderRow : ObservableObject
    {
        [ObservableProperty]
        private int _width = 600;

        [ObservableProperty]
        private int _height = 400;

        [ObservableProperty]
        private int _quantity = 4;

        [ObservableProperty]
        private bool _allowRotation = true;
    }
}
