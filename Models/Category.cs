using System.Text.Json.Serialization;

namespace e_commerce.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Relacionamento
        [JsonIgnore]
        public ICollection<Product>? Products { get; set; }
    }
}
