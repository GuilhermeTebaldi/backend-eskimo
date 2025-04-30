using System.Collections.Generic;

namespace e_commerce.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string DeliveryType { get; set; } = "retirar";
        public string? Address { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public string Store { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = "pendente"; // ou "pago", "entregue"
public string? PhoneNumber { get; set; } // WhatsApp do cliente
public decimal DeliveryFee { get; set; }  // 💸 valor calculado pela distância


        public List<OrderItem> Items { get; set; } = new();
    }
}
