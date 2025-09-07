using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using System;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentConfigsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaymentConfigsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/paymentconfigs
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _context.PaymentConfigs
                .OrderBy(p => p.Store)
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/paymentconfigs/{store}
        [HttpGet("{store}")]
        public async Task<IActionResult> GetByStore(string store)
        {
            if (string.IsNullOrWhiteSpace(store))
                return BadRequest(new { message = "Parâmetro 'store' é obrigatório." });

            var normalized = store.Trim();
            var config = await _context.PaymentConfigs
                .FirstOrDefaultAsync(p => p.Store == normalized);

            if (config == null)
                return NotFound(new { message = $"Nenhuma configuração encontrada para a loja '{normalized}'." });

            return Ok(config);
        }

        // PUT: api/paymentconfigs/{store}  (Upsert)
        [HttpPut("{store}")]
        public async Task<IActionResult> Upsert(string store, [FromBody] PaymentConfig body)
        {
            if (string.IsNullOrWhiteSpace(store))
                return BadRequest(new { message = "Parâmetro 'store' é obrigatório." });

            var normalized = store.Trim();

            // Segurança: garante que a Store do body seja a mesma da rota
            body.Store = normalized;
            body.UpdatedAt = DateTime.UtcNow;

            var existing = await _context.PaymentConfigs
                .FirstOrDefaultAsync(p => p.Store == normalized);

            if (existing == null)
            {
                // Cria novo
                _context.PaymentConfigs.Add(body);
            }
            else
            {
                // Atualiza campos relevantes
                existing.Cnpj = body.Cnpj;
                existing.Provider = string.IsNullOrWhiteSpace(body.Provider) ? existing.Provider : body.Provider;

                existing.MpPublicKey = body.MpPublicKey;
                existing.MpAccessToken = body.MpAccessToken;

                existing.PixKey = body.PixKey;
                existing.BankName = body.BankName;
                existing.BankClientId = body.BankClientId;
                existing.BankClientSecret = body.BankClientSecret;
                existing.BankCertPath = body.BankCertPath;
                existing.BankCertPassword = body.BankCertPassword;

                existing.IsActive = body.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Configuração de pagamento salva com sucesso.", store = normalized });
        }

        // DELETE: api/paymentconfigs/{store}
        [HttpDelete("{store}")]
        public async Task<IActionResult> Delete(string store)
        {
            if (string.IsNullOrWhiteSpace(store))
                return BadRequest(new { message = "Parâmetro 'store' é obrigatório." });

            var normalized = store.Trim();

            var existing = await _context.PaymentConfigs
                .FirstOrDefaultAsync(p => p.Store == normalized);

            if (existing == null)
                return NotFound(new { message = $"Nenhuma configuração encontrada para a loja '{normalized}'." });

            _context.PaymentConfigs.Remove(existing);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Configuração removida com sucesso.", store = normalized });
        }
    }
}
