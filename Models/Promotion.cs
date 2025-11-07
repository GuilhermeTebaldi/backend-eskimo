using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class Promotion
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public decimal PreviousPrice { get; set; }

        public decimal CurrentPrice { get; set; }

        public string? HighlightText { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }
    }
}
