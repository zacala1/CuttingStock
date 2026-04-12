namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// Mutable row type for the stock DataGrid.
    /// Keeps property names (Length, Quantity) identical to the XAML bindings.
    /// </summary>
    public sealed class StockRow
    {
        public int Length { get; set; } = 12000;
        public int Quantity { get; set; } = 1;
    }
}
