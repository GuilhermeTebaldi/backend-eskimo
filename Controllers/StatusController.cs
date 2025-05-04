using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;

namespace CSharpAssistant.API.Models

{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StatusController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
   public async Task<IActionResult> GetStatus()
{
    var dbOk = await _context.Database.CanConnectAsync();
    return Ok(new
    {
        message = "🟢 API e-commerce está rodando!",
        dbStatus = dbOk ? "🟢 DB conectado" : "🔴 DB com erro"
    });
}

    }
}
