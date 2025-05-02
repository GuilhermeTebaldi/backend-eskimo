using CSharpAssistant.API.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models

{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int Stock { get; set; }
       public List<StoreProductVisibility> Visibilities { get; set; } = new();




        // Relacionamento
        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public int? SubcategoryId { get; set; }

[ForeignKey("SubcategoryId")]
public Subcategory? Subcategory { get; set; }



    }
}
