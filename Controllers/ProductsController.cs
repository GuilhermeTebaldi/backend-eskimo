// CSharpAssistant.API/Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CSharpAssistant.API.DTOs;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Services;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ProductService _productService;

        public ProductsController(AppDbContext context, ProductService productService)
        {
            _context = context;
            _productService = productService;
        }

        // GET: api/products/list?store=efapi
        [HttpGet("list")]
        public IActionResult GetFiltered(
            [FromQuery] string? name,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] string? store = null)
        {
            var result = _productService.GetAllProducts(name, page, pageSize, store)
                .OrderByDescending(p => p.PinnedTop ?? false)
                .ThenBy(p => p.SortRank ?? int.MaxValue)
                .ThenBy(p => p.Name);

            return Ok(result);
        }

        // POST: api/products
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = new Product
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CategoryId = product.CategoryId,
                SubcategoryId = product.SubcategoryId
            };

            _context.Products.Add(entity);
            await _context.SaveChangesAsync();

            foreach (var store in new[] { "efapi", "palmital", "passo" })
            {
                _context.StoreStocks.Add(new StoreStock { ProductId = entity.Id, Store = store, Quantity = 0 });
            }
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        // PUT: api/products/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Product updated)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.Name = updated.Name;
            product.Description = updated.Description;
            product.Price = updated.Price;
            product.ImageUrl = updated.ImageUrl;
            product.CategoryId = updated.CategoryId;
            product.SubcategoryId = updated.SubcategoryId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE: api/products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/products/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .Include(p => p.Visibilities)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            var dto = new ProductDTO
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Stock = 0,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name,
                SubcategoryId = product.SubcategoryId,
                SubcategoryName = product.Subcategory?.Name,
                SortRank = product.SortRank,
                PinnedTop = product.PinnedTop,
                StoreStocks = product.StoreStocks?
                    .GroupBy(s => s.Store)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.Quantity).FirstOrDefault()),
                Visibilities = product.Visibilities != null && product.Visibilities.Any()
                    ? product.Visibilities.GroupBy(v => v.Store)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.IsVisible).FirstOrDefault())
                    : null
            };

            return Ok(dto);
        }

        // GET: api/products/5/visibility
        [HttpGet("{id}/visibility")]
        public async Task<IActionResult> GetVisibility(int id)
        {
            var stores = await _context.StoreProductVisibilities
                .Where(v => v.ProductId == id)
                .Select(v => v.Store)
                .ToListAsync();

            return Ok(stores);
        }

        // POST: api/products/5/visibility
        [HttpPost("{id}/visibility")]
        public async Task<IActionResult> SetVisibility(int id, [FromBody] List<string> stores)
        {
            var product = await _context.Products
                .Include(p => p.Visibilities)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();

            if (product.Visibilities != null && product.Visibilities.Any())
                _context.StoreProductVisibilities.RemoveRange(product.Visibilities);

            foreach (var store in stores.Distinct())
            {
                _context.StoreProductVisibilities.Add(new StoreProductVisibility
                {
                    ProductId = id,
                    Store = store,
                    IsVisible = true
                });
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // PUT: /api/storefront/layout
        [HttpPut("~/api/storefront/layout")]
        public async Task<IActionResult> UpdateStorefrontLayout([FromBody] StorefrontLayoutPayload payload)
        {
            if (payload?.Items == null || payload.Items.Count == 0) return BadRequest("Payload vazio.");

            var ids = payload.Items.Keys.ToList();
            var products = await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync();

            foreach (var p in products)
            {
                if (!payload.Items.TryGetValue(p.Id, out var it)) continue;
                if (it.SortRank.HasValue) p.SortRank = it.SortRank;
                if (it.PinnedTop.HasValue) p.PinnedTop = it.PinnedTop;
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = products.Count });
        }

        public class StorefrontLayoutPayload
        {
            public Dictionary<int, StorefrontLayoutItem> Items { get; set; } = new();
        }
        public class StorefrontLayoutItem
        {
            public int? SortRank { get; set; }
            public bool? PinnedTop { get; set; }
        }
    }
}
