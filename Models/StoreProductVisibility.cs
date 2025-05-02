using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class StoreProductVisibility
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public string Store { get; set; } = string.Empty;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }
}
