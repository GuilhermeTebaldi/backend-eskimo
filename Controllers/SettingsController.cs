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
                return NotFound(new { message = "Nenhuma configuração encontrada." });

            return Ok(setting);
        }

        // PUT: api/settings
        [HttpPut]
        public async Task<IActionResult> UpdateSetting([FromBody] Setting updated)
        {
            var setting = await _context.Settings.FirstOrDefaultAsync();
            if (setting == null)
            {
                updated.CreatedAt = DateTime.UtcNow;
                updated.UpdatedAt = DateTime.UtcNow;
                _context.Settings.Add(updated);
            }
            else
            {
                setting.DeliveryRate = updated.DeliveryRate;
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
                return NotFound(new { message = "Nenhuma configuração para excluir." });

            _context.Settings.Remove(setting);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuração removida com sucesso." });
        }
    }
}
