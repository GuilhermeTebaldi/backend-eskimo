using System;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/store-settings")]
    public class StoreSettingsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public StoreSettingsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("{store}")]
        public async Task<IActionResult> Get(string store)
        {
            var s = store?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s))
            {
                return BadRequest(new { error = "Loja inválida." });
            }

            var cfg = await _db.Set<StoreSetting>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Store == s);

            return cfg == null
                ? NotFound(new { message = "Loja sem configuração." })
                : Ok(cfg);
        }

        [HttpPut("{store}")]
        public async Task<IActionResult> Put(string store, [FromBody] StoreSetting body)
        {
            var s = store?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(s))
            {
                return BadRequest(new { error = "Loja inválida." });
            }

            if (body == null)
            {
                return BadRequest(new { error = "Body inválido." });
            }

            var cfg = await _db.Set<StoreSetting>()
                .FirstOrDefaultAsync(x => x.Store == s);

            if (cfg == null)
            {
                cfg = new StoreSetting
                {
                    Store = s,
                    TimeZone = string.IsNullOrWhiteSpace(body.TimeZone)
                        ? "America/Sao_Paulo"
                        : body.TimeZone.Trim(),
                    OpeningHoursJson = string.IsNullOrWhiteSpace(body.OpeningHoursJson)
                        ? "{}"
                        : body.OpeningHoursJson.Trim(),
                    ExceptionsJson = string.IsNullOrWhiteSpace(body.ExceptionsJson)
                        ? "[]"
                        : body.ExceptionsJson.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _db.AddAsync(cfg);
            }
            else
            {
                cfg.TimeZone = string.IsNullOrWhiteSpace(body.TimeZone)
                    ? cfg.TimeZone
                    : body.TimeZone.Trim();
                cfg.OpeningHoursJson = string.IsNullOrWhiteSpace(body.OpeningHoursJson)
                    ? cfg.OpeningHoursJson
                    : body.OpeningHoursJson.Trim();
                cfg.ExceptionsJson = string.IsNullOrWhiteSpace(body.ExceptionsJson)
                    ? cfg.ExceptionsJson
                    : body.ExceptionsJson.Trim();
                cfg.UpdatedAt = DateTime.UtcNow;
                _db.Update(cfg);
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Configuração da loja atualizada." });
        }
    }
}
