using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models

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
