using System;
using System.Linq;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using CSharpAssistant.API.Hubs;
using CSharpAssistant.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/promotions")]
    public class PromotionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<UpdateHub> _hubContext;

        public PromotionsController(AppDbContext context, IHubContext<UpdateHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet("active")]
        [HttpGet] // GET /api/promotions
        public async Task<IActionResult> GetActivePromotion()
        {
            var promotion = await _context.Promotions
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.UpdatedAt)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Category)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Subcategory)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.StoreStocks!)
                .Include(p => p.Product)
                    .ThenInclude(p => p!.Visibilities!)
                .FirstOrDefaultAsync();

            if (promotion == null)
            {
                return Ok(null);
            }

            var store = ExtractStoreFromRequest();
            return Ok(MapPromotion(promotion, store));
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetActivePromotions()
        {
            var store = ExtractStoreFromRequest();
            var list = await _context.Promotions
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.UpdatedAt)
                .Include(p => p.Product).ThenInclude(p => p!.Category)
                .Include(p => p.Product).ThenInclude(p => p!.Subcategory)
                .Include(p => p.Product).ThenInclude(p => p!.StoreStocks!)
                .Include(p => p.Product).ThenInclude(p => p!.Visibilities!)
                .ToListAsync();

            return Ok(list.Select(p => MapPromotion(p, store)).ToList());
        }

        [HttpPut("active")]
        [HttpPut] // PUT /api/promotions
        public async Task<IActionResult> UpsertPromotion([FromBody] PromotionUpsertRequest body)
        {
            if (body.ProductId <= 0)
            {
                return BadRequest("Produto inválido.");
            }

            if (body.CurrentPrice <= 0)
            {
                return BadRequest("Preço promocional inválido.");
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .Include(p => p.Visibilities)
                .FirstOrDefaultAsync(p => p.Id == body.ProductId);

            if (product == null)
            {
                return NotFound("Produto não encontrado.");
            }

            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.IsActive && p.ProductId == body.ProductId);

            if (promotion == null)
            {
                promotion = new Promotion
                {
                    CreatedAt = DateTime.UtcNow,
                };
                _context.Promotions.Add(promotion);
            }

            var previousPrice = body.PreviousPrice ?? product.Price;

            promotion.ProductId = body.ProductId;
            promotion.PreviousPrice = previousPrice;
            promotion.CurrentPrice = body.CurrentPrice;
            promotion.HighlightText = string.IsNullOrWhiteSpace(body.HighlightText)
                ? null
                : body.HighlightText.Trim();
            promotion.IsActive = true;
            promotion.UpdatedAt = DateTime.UtcNow;

            if (body.UpdateProductPrice)
            {
                product.Price = body.CurrentPrice;
            }

            await _context.SaveChangesAsync();

            promotion = await _context.Promotions
                .Include(p => p.Product).ThenInclude(p => p!.Category)
                .Include(p => p.Product).ThenInclude(p => p!.Subcategory)
                .Include(p => p.Product).ThenInclude(p => p!.StoreStocks!)
                .Include(p => p.Product).ThenInclude(p => p!.Visibilities!)
                .FirstAsync(p => p.Id == promotion.Id);

            await _hubContext.Clients.All.SendAsync("dataUpdated", "promotion");
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");

            var store = ExtractStoreFromRequest();
            return Ok(MapPromotion(promotion, store));
        }

        [HttpDelete("active")]
        [HttpDelete] // DELETE /api/promotions
        public async Task<IActionResult> DisablePromotion()
        {
            var promotion = await _context.Promotions.FirstOrDefaultAsync(p => p.IsActive);
            if (promotion == null)
            {
                return NoContent();
            }

            promotion.IsActive = false;
            promotion.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("dataUpdated", "promotion");
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteById([FromRoute] int id)
        {
            var item = await _context.Promotions.FirstOrDefaultAsync(p => p.Id == id);
            if (item is null) return NotFound();
            _context.Promotions.Remove(item);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "promotion");
            return NoContent();
        }

        private string? ExtractStoreFromRequest()
        {
            string? store = HttpContext.Request.Headers["X-Store"].FirstOrDefault()
                ?? HttpContext.Request.Query["store"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(store) ? null : store.Trim().ToLowerInvariant();
        }

        private static PromotionDTO MapPromotion(Promotion promotion, string? store = null)
        {
            int? stockValue = null;
            if (!string.IsNullOrWhiteSpace(store))
            {
                stockValue = promotion.Product?.StoreStocks?
                    .FirstOrDefault(s => s.Store != null && s.Store.Equals(store, StringComparison.OrdinalIgnoreCase))
                    ?.Quantity;
            }

            stockValue ??= promotion.Product?.StoreStocks?.Sum(s => s.Quantity) ?? promotion.Product?.Stock;

            return new PromotionDTO
            {
                Id = promotion.Id,
                ProductId = promotion.ProductId,
                PreviousPrice = promotion.PreviousPrice,
                CurrentPrice = promotion.CurrentPrice,
                HighlightText = promotion.HighlightText,
                IsActive = promotion.IsActive,
                CreatedAt = promotion.CreatedAt,
                UpdatedAt = promotion.UpdatedAt,
                Product = promotion.Product == null
                    ? null
                    : new ProductDTO
                    {
                        Id = promotion.Product.Id,
                        Name = promotion.Product.Name,
                        Description = promotion.Product.Description,
                        Price = promotion.Product.Price,
                        ImageUrl = promotion.Product.ImageUrl,
                        Stock = stockValue ?? 0,
                        CategoryId = promotion.Product.CategoryId,
                        CategoryName = promotion.Product.Category?.Name,
                        SubcategoryId = promotion.Product.SubcategoryId,
                        SubcategoryName = promotion.Product.Subcategory?.Name,
                        SortRank = promotion.Product.SortRank,
                        PinnedTop = promotion.Product.PinnedTop,
                        IsArchived = promotion.Product.IsArchived,
                        StoreStocks = promotion.Product.StoreStocks?.ToDictionary(s => s.Store, s => s.Quantity),
                        Visibilities = promotion.Product.Visibilities?.ToDictionary(v => v.Store, v => v.IsVisible)
                    }
            };
        }

        public class PromotionUpsertRequest
        {
            public int ProductId { get; set; }
            public decimal CurrentPrice { get; set; }
            public decimal? PreviousPrice { get; set; }
            public string? HighlightText { get; set; }
            public bool UpdateProductPrice { get; set; } = true;
        }
    }
}
