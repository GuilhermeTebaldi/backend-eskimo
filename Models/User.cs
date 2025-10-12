using System.ComponentModel.DataAnnotations.Schema;

namespace CSharpAssistant.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "operator"; // padrão

        // JSON com permissões por ação/loja. Ex.: {"can_manage_products":true,"stores":{"efapi":{"orders":true}}}
        [Column(TypeName = "jsonb")]
        public string Permissions { get; set; } = "{}";

        // Admin pode desabilitar login de qualquer usuário
        public bool IsEnabled { get; set; } = true;
    }
}
