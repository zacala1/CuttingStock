namespace CuttingStock.Models
{
    /// <summary>
    /// 재고 철근을 나타내는 클래스
    /// </summary>
    public class RebarStock
    {
        public int Length { get; set; }
        public int Quantity { get; set; }

        public RebarStock()
        {
        }

        public RebarStock(int length, int quantity)
        {
            Length = length;
            Quantity = quantity;
        }
    }
}