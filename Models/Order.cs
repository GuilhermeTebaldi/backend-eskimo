using System;
using System.Collections.Generic;

namespace CSharpAssistant.API.Models

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
    public string PaymentMethod { get; set; } = "mercado_pago";
    public string? PhoneNumber { get; set; } // WhatsApp do cliente
    public decimal DeliveryFee { get; set; }  // 💸 valor calculado pela distância
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? WhatsappNotifiedAt { get; set; }

    public DateTime? PrintedAtUtc { get; set; }
    public string? PrintReason { get; set; }
    public string? PrintedBy { get; set; }
    public int? PrintCopies { get; set; }
    public string? LastPrintError { get; set; }

    public int? StoreCustomerId { get; set; }
    public StoreCustomer? StoreCustomer { get; set; }

    public List<OrderItem> Items { get; set; } = new();
}
}
