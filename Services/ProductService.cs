using e_commerce.Data;
using e_commerce.DTOs;
using Microsoft.EntityFrameworkCore;

namespace e_commerce.Services
{
    public class ProductService
    {
        private readonly AppDbContext _context;

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        public IEnumerable<ProductDTO> GetAllProducts(string? nameFilter = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Products
                .Include(p => p.Category) // Inclui o nome da categoria
                .AsQueryable();

            if (!string.IsNullOrEmpty(nameFilter))
            {
                nameFilter = nameFilter.ToLower(); // ⬅️ força minúsculas
                query = query.Where(p => p.Name.ToLower().Contains(nameFilter));
            }

            return query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    Stock = p.Stock,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .ToList();
        }
    }
}
