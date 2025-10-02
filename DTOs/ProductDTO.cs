// CSharpAssistant.API/DTOs/ProductDTO.cs
using System.Collections.Generic;

namespace CSharpAssistant.API.DTOs
{
    public class ProductDTO
    {
        // Core
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }

        // Compat: usado quando ?store= está presente em /products/list
        public int Stock { get; set; }

        // Cat/Subcat
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? SubcategoryId { get; set; }
        public string? SubcategoryName { get; set; }

        // Layout
        public int? SortRank { get; set; }
        public bool? PinnedTop { get; set; }

        // Multiloja
        public Dictionary<string, int>? StoreStocks { get; set; }
        public Dictionary<string, bool>? Visibilities { get; set; }
    }
}
