namespace CSharpAssistant.API.Models
{
    public class StoreProductVisibility
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Store { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
        public int SortRank { get; set; } = 0;
        public bool PinnedTop { get; set; } = false;
        public string? StyleJson { get; set; }
        public Product? Product { get; set; }
    }
}
