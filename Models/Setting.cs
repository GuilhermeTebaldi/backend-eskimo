using System;
using System.ComponentModel.DataAnnotations;

namespace CSharpAssistant.API.Models

{
   public class Setting
{
    public int Id { get; set; }

    [Required]
    public decimal DeliveryRate { get; set; }

    // ✅ Novo: valor mínimo de entrega
    public decimal MinDelivery { get; set; } = 0m;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

}
