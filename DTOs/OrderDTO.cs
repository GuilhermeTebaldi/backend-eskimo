using System;
using System.Collections.Generic;

namespace CSharpAssistant.API.DTOs
{
    public class OrderDTO
    {
        public int Id { get; set; }

        public string Store { get; set; }

        public string Name { get; set; }

        public string PhoneNumber { get; set; }

        public string DeliveryType { get; set; }

        public string Address { get; set; }

        public decimal Total { get; set; }

        public decimal DeliveryFee { get; set; }

        public string Status { get; set; }
        // DTOs/OrderDTO.cs

public string? CustomerName { get; set; }
public string? Street { get; set; }
public string? Number { get; set; }
public string? Complement { get; set; }


        public DateTime CreatedAt { get; set; }

        public List<OrderItemDTO> Items { get; set; }
        
    }
}
