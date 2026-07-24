namespace CuttingStock.UI.Services
{
    public readonly record struct LengthQuantityInput(int Length, int Quantity);
    public readonly record struct SheetInput(int Width, int Height, int Quantity);
    public readonly record struct RectOrderInput(
        int Width,
        int Height,
        int Quantity,
        bool AllowRotation);
}
