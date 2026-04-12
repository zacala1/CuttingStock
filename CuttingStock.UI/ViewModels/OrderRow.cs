namespace CuttingStock.UI.ViewModels
{
    /// <summary>
    /// Mutable row type for the order DataGrid.
    /// Keeps property names (Length, Quantity) identical to the XAML bindings.
    /// </summary>
    public sealed class OrderRow
    {
        public int Length { get; set; } = 5000;
        public int Quantity { get; set; } = 1;
    }
}
