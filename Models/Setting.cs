using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class Setting
    {
        public int Id { get; set; }

        [Required]
        public decimal DeliveryRate { get; set; }

        // ✅ Novo: valor mínimo de entrega
        public decimal MinDelivery { get; set; } = 0m;

        // === Horário de funcionamento ===
        // Timezone IANA. Ex.: "America/Sao_Paulo"
        [MaxLength(64)]
        public string TimeZone { get; set; } = "America/Sao_Paulo";

        // Grade semanal em JSON (jsonb). Formato:
        // {"monday":[{"start":"09:00","end":"18:00"}], "tuesday":[...], ...}
        // Campos em minúsculas: "monday"..."sunday"
        [Column(TypeName = "jsonb")]
        public string OpeningHoursJson { get; set; } = "{}";

        // Exceções por data em JSON (jsonb). Formato:
        // [{"date":"2025-12-25","closed":true},
        //  {"date":"2025-12-24","ranges":[{"start":"08:00","end":"12:00"}]}]
        [Column(TypeName = "jsonb")]
        public string ExceptionsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
