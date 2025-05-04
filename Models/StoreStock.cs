using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class StoreStock
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string Store { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public Product? Product { get; set; }
    }
}
