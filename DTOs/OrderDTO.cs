namespace e_commerce.DTOs
{
    public class OrderDTO
    {
        public string CustomerName { get; set; } = string.Empty;
        public string DeliveryType { get; set; } = "retirar";
        public string? Address { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string Store { get; set; } = string.Empty;
        public decimal Total { get; set; }

        public string? PhoneNumber { get; set; } // ✅ CORRETO AQUI!

        public List<OrderItemDTO> Items { get; set; } = new();
    }

    public class OrderItemDTO
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
