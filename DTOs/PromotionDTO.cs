using System;

namespace CSharpAssistant.API.DTOs
{
    public class PromotionDTO
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public bool IsActive { get; set; }
        public string? HighlightText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public ProductDTO? Product { get; set; }
    }
}
