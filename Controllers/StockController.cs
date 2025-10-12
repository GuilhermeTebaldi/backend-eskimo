// CSharpAssistant.API/Controllers/StockController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using Microsoft.AspNetCore.Authorization;


namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("[controller]")] // compat opcional
    [Authorize(Policy = "RequireOperatorOrAdmin")]
public class StockController : ControllerBase

    {
        private readonly AppDbContext _context;
        public StockController(AppDbContext context) { _context = context; }

        // GET: /api/stock
        [HttpGet]
        public async Task<IActionResult> GetAllStocks()
        {
            var products = await _context.Products.AsNoTracking().ToListAsync();
            var stocks = await _context.StoreStocks.AsNoTracking().ToListAsync();

            var result = products.Select(p => new
            {
                productId = p.Id,
                productName = p.Name,
                imageUrl = p.ImageUrl,
                efapi = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "efapi")?.Quantity ?? 0,
                palmital = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "palmital")?.Quantity ?? 0,
                passo  = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "passo")?.Quantity ?? 0
            });

            return Ok(result);
        }

        // POST: /api/stock/{productId}
        [HttpPost("{productId}")]
        public Task<IActionResult> UpdateStockPost(int productId, [FromBody] Dictionary<string, int> payload)
            => UpsertStock(productId, payload);

        // PUT: /api/stock/{productId}
        [HttpPut("{productId}")]
        public Task<IActionResult> UpdateStockPut(int productId, [FromBody] Dictionary<string, int> payload)
            => UpsertStock(productId, payload);

        private async Task<IActionResult> UpsertStock(int productId, Dictionary<string, int> payload)
        {
            if (payload == null) return BadRequest("Payload inválido.");

            var productExists = await _context.Products.AnyAsync(p => p.Id == productId);
            if (!productExists) return NotFound();

            // Normaliza chaves do payload para minúsculas
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["efapi"] = 0,
                ["palmital"] = 0,
                ["passo"] = 0
            };
            foreach (var kv in payload)
                map[kv.Key.ToLower()] = kv.Value;

            foreach (var store in new[] { "efapi", "palmital", "passo" })
            {
                var qty = map.TryGetValue(store, out var v) ? v : 0;

                var row = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.Store.ToLower() == store);

                if (row == null)
                    _context.StoreStocks.Add(new StoreStock { ProductId = productId, Store = store, Quantity = qty });
                else
                    row.Quantity = qty;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Estoque atualizado." });
        }
    }
}
