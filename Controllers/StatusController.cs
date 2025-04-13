using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using e_commerce.Data;

namespace e_commerce.Controllers
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
