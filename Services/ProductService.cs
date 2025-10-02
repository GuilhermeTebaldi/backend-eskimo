// CSharpAssistant.API/Services/ProductService.cs
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace CSharpAssistant.API.Services
{
    public class ProductService
    {
        private readonly AppDbContext _context;
        public ProductService(AppDbContext context) => _context = context;

        // Sem store => lista todos. Com store => apenas itens com estoque > 0 nessa loja.
        public IEnumerable<ProductDTO> GetAllProducts(string? nameFilter = null, int page = 1, int pageSize = 10, string? store = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .Include(p => p.Visibilities)
                .AsQueryable();

            if (!string.IsNullOrEmpty(store))
            {
                query = query.Where(p => p.StoreStocks.Any(s => s.Store == store && s.Quantity > 0));
            }

            if (!string.IsNullOrEmpty(nameFilter))
            {
                var nf = nameFilter.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(nf) ||
                    (p.Description != null && p.Description.ToLower().Contains(nf)));
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

                    // compat front quando ?store= estiver presente
                    Stock = store != null
                        ? p.StoreStocks.Where(s => s.Store == store).Select(s => s.Quantity).FirstOrDefault()
                        : 0,

                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    SubcategoryId = p.SubcategoryId,
                    SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null,

                    // necessário para ordenação na controller
                    SortRank = p.SortRank,
                    PinnedTop = p.PinnedTop,

                    // necessários para o Admin
                    StoreStocks = p.StoreStocks
                        .GroupBy(s => s.Store)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Quantity).FirstOrDefault()),

                    Visibilities = p.Visibilities != null && p.Visibilities.Any()
                        ? p.Visibilities
                            .GroupBy(v => v.Store)
                            .ToDictionary(g => g.Key, g => g.Select(x => x.IsVisible).FirstOrDefault())
                        : null
                })
                .ToList();
        }
    }
}
