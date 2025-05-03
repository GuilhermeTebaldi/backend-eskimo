using CSharpAssistant.API.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using e_commerce.Data;
using e_commerce.Models;

namespace e_commerce.Controllers
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
            var stocks = await _context.StoreStocks
                .Include(s => s.Product)
                .ToListAsync();

            var result = stocks
                .GroupBy(s => s.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    ProductName = group.First().Product.Name,
                    ImageUrl = group.First().Product.ImageUrl,
                    Efapi = group.FirstOrDefault(s => s.Store == "efapi")?.Quantity ?? 0,
                    Palmital = group.FirstOrDefault(s => s.Store == "palmital")?.Quantity ?? 0,
                    Passo = group.FirstOrDefault(s => s.Store == "passo")?.Quantity ?? 0
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

                // Atualizar estoque
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

                // Atualizar visibilidade automaticamente com base no estoque
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
