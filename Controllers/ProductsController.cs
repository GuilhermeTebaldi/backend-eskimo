// CSharpAssistant.API/Controllers/ProductsController.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using CSharpAssistant.API.Hubs;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Route("[controller]")] // compat opcional se baseURL já inclui /api
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ProductService _productService;
        private readonly IHubContext<UpdateHub> _hubContext;

        public ProductsController(
            AppDbContext context,
            ProductService productService,
            IHubContext<UpdateHub> hubContext)
        {
            _context = context;
            _productService = productService;
            _hubContext = hubContext;
        }

        [HttpGet("list")]
        public IActionResult GetFiltered(
            [FromQuery] string? name,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100,
            [FromQuery] string? store = null,
            [FromQuery] bool includeArchived = false)
        {
            var result = _productService.GetAllProducts(name, page, pageSize, store, includeArchived);
            return Ok(result);
        }

        // 📦 POST: /api/products
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var entity = new Product
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CategoryId = product.CategoryId,
                SubcategoryId = product.SubcategoryId,
                SortRank = product.SortRank,
                PinnedTop = product.PinnedTop
            };

            _context.Products.Add(entity);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");

            foreach (var store in new[] { "efapi", "palmital", "passo" })
            {
                _context.StoreStocks.Add(new StoreStock { ProductId = entity.Id, Store = store, Quantity = 0 });
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, entity);
        }

        // 🛠 PUT: /api/products/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Product updated)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            product.Name = updated.Name;
            product.Description = updated.Description;
            product.Price = updated.Price;
            product.ImageUrl = updated.ImageUrl;
            product.CategoryId = updated.CategoryId;
            product.SubcategoryId = updated.SubcategoryId;
            product.SortRank = updated.SortRank;
            product.PinnedTop = updated.PinnedTop;

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");
            return Ok(new { message = "Produto atualizado com sucesso." });
        }

        // 🗑 DELETE: /api/products/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");
            return NoContent();
        }

        // 📦 GET: /api/products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

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
                SubcategoryName = product.Subcategory?.Name
            };

            return Ok(dto);
        }

        // 👁️‍🗨️ GET: /api/products/{id}/visibility
        [HttpGet("{id}/visibility")]
        public async Task<IActionResult> GetVisibility(int id)
        {
            var stores = await _context.StoreProductVisibilities
                .Where(v => v.ProductId == id)
                .Select(v => v.Store)
                .ToListAsync();

            return Ok(stores);
        }

        // ✅ POST: /api/products/{id}/visibility
        [HttpPost("{id}/visibility")]
        public async Task<IActionResult> SetVisibility(int id, [FromBody] List<string> stores)
        {
            var product = await _context.Products
                .Include(p => p.Visibilities)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            if (product.Visibilities != null && product.Visibilities.Any())
                _context.StoreProductVisibilities.RemoveRange(product.Visibilities);

            foreach (var store in stores)
            {
                _context.StoreProductVisibilities.Add(new StoreProductVisibility
                {
                    ProductId = id,
                    Store = store,
                    IsVisible = true
                });
            }

            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");
            return Ok();
        }

        // 🗄 Arquivar / Recolocar produto
        [HttpPatch("{id}/archive")]
        public async Task<IActionResult> Archive(int id, [FromBody] ArchiveRequest body)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsArchived = body.IsArchived;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("dataUpdated", "products");
            return NoContent();
        }

        public class ArchiveRequest
        {
            public bool IsArchived { get; set; }
        }

    }
}
