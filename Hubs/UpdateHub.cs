using Microsoft.AspNetCore.SignalR;

namespace CSharpAssistant.API.Hubs
{
    public class UpdateHub : Hub
    {
        // Opcional: pode registrar logs de conexão
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }
    }
}
