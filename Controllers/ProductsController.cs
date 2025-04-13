using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using e_commerce.DTOs;
using e_commerce.Data;
using e_commerce.Models;
using e_commerce.Services;

namespace e_commerce.Controllers
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

        // 📦 GET: api/products/list
        [HttpGet("list")]
        public IActionResult GetFiltered(
            [FromQuery] string? name,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            var result = _productService.GetAllProducts(name, page, pageSize);
            return Ok(result);
        }

        // 📦 POST: api/products
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        // 🛠 PUT: api/products/5
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
            product.Stock = updated.Stock;
            product.CategoryId = updated.CategoryId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 🗑 DELETE: api/products/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound();

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 📦 GET: api/products/5 (mantido com DTO e Include)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
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
                Stock = product.Stock,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name
            };

            return Ok(dto);
        }
    }
}
