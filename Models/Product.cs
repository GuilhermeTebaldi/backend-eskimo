// CSharpAssistant.API/Models/Product.cs  — original + 2 campos de layout
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }
        
        public string? Description { get; set; }
        
        public decimal Price { get; set; }
        
        public string? ImageUrl { get; set; }
        
        public int? Stock { get; set; }

        public int? CategoryId { get; set; }

        public int? SubcategoryId { get; set; }

        // ↓↓↓ restaurados para compatibilidade com StorefrontController
        public int? SortRank { get; set; }
        public bool? PinnedTop { get; set; }

        public Category? Category { get; set; }

        public Subcategory? Subcategory { get; set; }

        public ICollection<StoreStock>? StoreStocks { get; set; }

        public ICollection<StoreProductVisibility>? Visibilities { get; set; }

        // Arquivamento lógico
        public bool IsArchived { get; set; } = false;
    }
}
