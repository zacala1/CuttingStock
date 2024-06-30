namespace CuttingStock.Models
{
    public class Order
    {
        public int Length { get; set; }
        public int Quantity { get; set; }

        public Order()
        {
        }

        public Order(int length, int quantity)
        {
            Length = length;
            Quantity = quantity;
        }
    }
}