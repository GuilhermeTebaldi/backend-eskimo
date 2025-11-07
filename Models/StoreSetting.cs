using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class StoreSetting
    {
        public int Id { get; set; }

        [Required, MaxLength(32)]
        public string Store { get; set; } = ""; // "efapi" | "palmital" | "passo"

        [MaxLength(64)]
        public string TimeZone { get; set; } = "America/Sao_Paulo";

        [Column(TypeName = "jsonb")]
        public string OpeningHoursJson { get; set; } = "{}";

        [Column(TypeName = "jsonb")]
        public string ExceptionsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
