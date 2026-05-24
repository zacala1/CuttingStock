using CommunityToolkit.Mvvm.ComponentModel;

namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// Mutable row type for the stock DataGrid.
    /// ObservableObject so DataGrid edits raise PropertyChanged — needed once we
    /// drive UI state from a ViewModel rather than reading the collection on
    /// every action handler.
    /// </summary>
    public sealed partial class StockRow : ObservableObject
    {
        [ObservableProperty]
        private int _length = 12000;

        [ObservableProperty]
        private int _quantity = 1;
    }
}
