using Microsoft.AspNetCore.Mvc;
using CSharpAssistant.API.Data;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

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

        [HttpGet("isOpen")]
        public async Task<IActionResult> GetIsOpen([FromServices] Data.AppDbContext db)
        {
            var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
            if (setting == null)
            {
                // Sem settings configurado: considera aberto.
                return Ok(new { isOpen = true, message = "Sem configuração. Considerado aberto." });
            }

            var payload = StatusControllerStatic.EvaluateStatus(setting.TimeZone, setting.OpeningHoursJson, setting.ExceptionsJson);
            return Ok(payload);
        }

        [HttpGet("isOpen/{store}")]
        public async Task<IActionResult> GetIsOpenForStore(
            [FromRoute] string store,
            [FromServices] AppDbContext db)
        {
            var s = store?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s))
            {
                return BadRequest(new { error = "Loja inválida." });
            }

            var setting = await db.Set<StoreSetting>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Store == s);

            if (setting == null)
            {
                return Ok(new { isOpen = true, message = "Sem configuração da loja. Considerado aberto." });
            }

            var payload = StatusControllerStatic.EvaluateStatus(setting.TimeZone, setting.OpeningHoursJson, setting.ExceptionsJson);
            return Ok(payload);
        }
    }
}
