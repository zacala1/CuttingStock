using CommunityToolkit.Mvvm.ComponentModel;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// Mutable row type for the 2D sheet DataGrid.
    /// </summary>
    public sealed partial class SheetRow : ObservableObject
    {
        [ObservableProperty]
        private int _width = 2440;

        [ObservableProperty]
        private int _height = 1220;

        [ObservableProperty]
        private int _quantity = 5;
    }
}
