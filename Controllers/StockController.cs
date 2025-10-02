// CSharpAssistant.API/Controllers/StockController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("[controller]")] // permite também /stock quando baseURL já inclui /api
    public class StockController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StockController(AppDbContext context) { _context = context; }

        // 🔎 GET: /api/stock  (retorna chaves minúsculas: efapi/palmital/passo)
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
                passo = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "passo")?.Quantity ?? 0
            });

            return Ok(result);
        }

        // 💾 POST: /api/stock/{productId}  (compatível com painel)
        [HttpPost("{productId}")]
        public Task<IActionResult> UpdateStockPost(int productId, [FromBody] Dictionary<string, int> payload)
            => UpdateStockInternal(productId, payload);

        // 💾 PUT: /api/stock/{productId}   (compat extra)
        [HttpPut("{productId}")]
        public Task<IActionResult> UpdateStockPut(int productId, [FromBody] Dictionary<string, int> payload)
            => UpdateStockInternal(productId, payload);

        private async Task<IActionResult> UpdateStockInternal(int productId, Dictionary<string, int> stocks)
        {
            if (stocks == null) return BadRequest("Payload inválido.");

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null) return NotFound();

            foreach (var store in new[] { "efapi", "palmital", "passo" })
            {
                var key = store.ToLower();
                var quantity = stocks.TryGetValue(key, out var q) ? q : 0;

                var existingStock = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.Store.ToLower() == key);

                if (existingStock != null)
                    existingStock.Quantity = quantity;
                else
                    _context.StoreStocks.Add(new StoreStock { ProductId = productId, Store = key, Quantity = quantity });

                var visibility = await _context.StoreProductVisibilities
                    .FirstOrDefaultAsync(v => v.ProductId == productId && v.Store.ToLower() == key);

                if (quantity > 0)
                {
                    if (visibility == null)
                        _context.StoreProductVisibilities.Add(new StoreProductVisibility { ProductId = productId, Store = key, IsVisible = true });
                    else
                        visibility.IsVisible = true;
                }
                else
                {
                    if (visibility != null)
                        _context.StoreProductVisibilities.Remove(visibility);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Estoque e visibilidade atualizados com sucesso!" });
        }
    }
}
