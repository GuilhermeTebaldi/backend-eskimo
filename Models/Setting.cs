using System;
using System.ComponentModel.DataAnnotations;

namespace CSharpAssistant.API.Models

{
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        public decimal DeliveryRate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
