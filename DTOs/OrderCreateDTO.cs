using System.Collections.Generic;

namespace CSharpAssistant.API.DTOs
{
    public class OrderCreateDTO
    {
        public string Store { get; set; }
        public string CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string DeliveryType { get; set; }
        public string? Address { get; set; }
        public string? Street { get; set; }
        public string? Number { get; set; }
        public string? Complement { get; set; }
        public decimal Total { get; set; }
        public decimal DeliveryFee { get; set; }

        public List<OrderItemDTO> Items { get; set; }
    }
}
