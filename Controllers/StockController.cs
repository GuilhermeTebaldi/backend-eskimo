using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StockController(AppDbContext context)
        {
            _context = context;
        }

        // 🔎 GET: /api/stock
        [HttpGet]
        public async Task<IActionResult> GetAllStocks()
        {
            var products = await _context.Products.ToListAsync();
            var stocks = await _context.StoreStocks.ToListAsync();

            var result = products.Select(p => new
            {
                ProductId = p.Id,
                ProductName = p.Name,
                ImageUrl = p.ImageUrl,
                Efapi = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "efapi")?.Quantity ?? 0,
                Palmital = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "palmital")?.Quantity ?? 0,
                Passo = stocks.FirstOrDefault(s => s.ProductId == p.Id && s.Store.ToLower() == "passo")?.Quantity ?? 0
            });

            return Ok(result);
        }

        // 💾 POST: /api/stock/{productId}
        [HttpPost("{productId}")]
        public async Task<IActionResult> UpdateStock(int productId, [FromBody] Dictionary<string, int> stocks)
        {
            foreach (var store in new[] { "efapi", "palmital", "passo" })
            {
                var quantity = stocks.ContainsKey(store) ? stocks[store] : 0;

                var existingStock = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.Store.ToLower() == store.ToLower());

                if (existingStock != null)
                {
                    existingStock.Quantity = quantity;
                }
                else
                {
                    _context.StoreStocks.Add(new StoreStock
                    {
                        ProductId = productId,
                        Store = store.ToLower(),
                        Quantity = quantity
                    });
                }

                var visibility = await _context.StoreProductVisibilities
                    .FirstOrDefaultAsync(v => v.ProductId == productId && v.Store.ToLower() == store.ToLower());

                if (quantity > 0)
                {
                    if (visibility == null)
                    {
                        _context.StoreProductVisibilities.Add(new StoreProductVisibility
                        {
                            ProductId = productId,
                            Store = store.ToLower()
                        });
                    }
                }
                else
                {
                    if (visibility != null)
                    {
                        _context.StoreProductVisibilities.Remove(visibility);
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Estoque e visibilidade atualizados com sucesso!" });
        }
    }
}
