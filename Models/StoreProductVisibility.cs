namespace CSharpAssistant.API.Models
{
    public class StoreProductVisibility
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string Store { get; set; } = string.Empty;

        public bool IsVisible { get; set; }

        public Product? Product { get; set; }
    }
}
