namespace CSharpAssistant.API.DTOs
{
    public class ProductDTO
    {
        public int Id { get; set; }
        
        public string Name { get; set; }

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public string? CategoryName { get; set; }

        public string? SubcategoryName { get; set; }

        public int? CategoryId { get; set; }

        public int? SubcategoryId { get; set; }
        public int? Stock { get; set; }


        public Dictionary<string, int>? StoreStocks { get; set; }

        public Dictionary<string, bool>? Visibilities { get; set; }
    }
}
