using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CSharpAssistant.API.Models;


namespace e_commerce.Models
{
    public class StoreStock
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Store { get; set; } = ""; // "efapi", "palmital", "passo"
        public int Quantity { get; set; }

        public Product Product { get; set; } = null!;
        
    }
}
