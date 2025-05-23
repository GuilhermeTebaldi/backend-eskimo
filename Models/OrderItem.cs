namespace CSharpAssistant.API.Models

{
    public class OrderItem
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }

        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
         public string? ImageUrl { get; set; }
         public string Store { get; set; } = string.Empty; // Loja que vendeu o produto

    }
}
