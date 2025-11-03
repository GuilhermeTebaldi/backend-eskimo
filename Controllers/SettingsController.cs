using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using System;
using System.Threading.Tasks;

namespace CSharpAssistant.API.Models
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/settings
        [HttpGet]
        public async Task<IActionResult> GetSetting()
        {
            var setting = await _context.Settings.FirstOrDefaultAsync();
            if (setting == null)
            {
                return NotFound(new { message = "Nenhuma configuração encontrada." });
            }

            return Ok(setting);
        }

        // PUT: api/settings
        [HttpPut]
        public async Task<IActionResult> UpdateSetting([FromBody] Setting updated)
        {
            // Normaliza entradas
            var safeRate = Math.Max(0m, updated.DeliveryRate);
            var safeMin = Math.Max(0m, updated.MinDelivery);

            var setting = await _context.Settings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new Setting
                {
                    DeliveryRate = safeRate,
                    MinDelivery = safeMin,
                    TimeZone = string.IsNullOrWhiteSpace(updated.TimeZone) ? "America/Sao_Paulo" : updated.TimeZone,
                    OpeningHoursJson = string.IsNullOrWhiteSpace(updated.OpeningHoursJson) ? "{}" : updated.OpeningHoursJson,
                    ExceptionsJson = string.IsNullOrWhiteSpace(updated.ExceptionsJson) ? "[]" : updated.ExceptionsJson,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Settings.Add(setting);
            }
            else
            {
                setting.DeliveryRate = safeRate;
                setting.MinDelivery = safeMin;
                setting.TimeZone = updated.TimeZone ?? setting.TimeZone;
                setting.OpeningHoursJson = string.IsNullOrWhiteSpace(updated.OpeningHoursJson)
                    ? setting.OpeningHoursJson
                    : updated.OpeningHoursJson;
                setting.ExceptionsJson = string.IsNullOrWhiteSpace(updated.ExceptionsJson)
                    ? setting.ExceptionsJson
                    : updated.ExceptionsJson;
                setting.UpdatedAt = DateTime.UtcNow;
                _context.Settings.Update(setting);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuração atualizada com sucesso." });
        }

        // DELETE: api/settings
        [HttpDelete]
        public async Task<IActionResult> DeleteSetting()
        {
            var setting = await _context.Settings.FirstOrDefaultAsync();
            if (setting == null)
            {
                return NotFound(new { message = "Nenhuma configuração para excluir." });
            }

            _context.Settings.Remove(setting);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuração removida com sucesso." });
        }

        [HttpGet("template")]
        public IActionResult GetTemplate()
        {
            var template = new
            {
                timeZone = "America/Sao_Paulo",
                openingHours = new
                {
                    monday = new[] { new { start = "09:00", end = "18:00" } },
                    tuesday = new[] { new { start = "09:00", end = "18:00" } },
                    wednesday = new[] { new { start = "09:00", end = "18:00" } },
                    thursday = new[] { new { start = "09:00", end = "18:00" } },
                    friday = new[] { new { start = "09:00", end = "18:00" } },
                    saturday = Array.Empty<object>(),
                    sunday = Array.Empty<object>()
                },
                exceptions = new object[]
                {
                    new { date = "2025-12-25", closed = true },
                    new { date = "2025-12-24", ranges = new[] { new { start = "08:00", end = "12:00" } } }
                }
            };
            return Ok(template);
        }
    }
}
