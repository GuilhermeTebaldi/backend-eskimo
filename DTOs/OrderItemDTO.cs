namespace CSharpAssistant.API.DTOs
{
    public class OrderItemDTO
    {
        public int ProductId { get; set; }
        public string? Name { get; set; }


        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }
    }
}
