// CSharpAssistant.API/Services/ProductService.cs
using System.Collections.Generic;
using System.Linq;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Services
{
    public class ProductService
    {
        private readonly AppDbContext _context;
        public ProductService(AppDbContext context) { _context = context; }

        // Comportamento anterior restaurado:
        // - Sem 'store' => lista todos.
        // - Com 'store' => lista apenas com Quantity > 0 nessa loja.
        public IList<ProductDTO> GetAllProducts(string? name, int page, int pageSize, string? store)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var q = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .Include(p => p.Visibilities)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var term = name.Trim();
                q = q.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
            }

            if (!string.IsNullOrEmpty(store))
            {
                q = q.Where(p => p.StoreStocks.Any(s => s.Store == store && s.Quantity > 0));
            }

            return q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,

                    // compat quando ?store= estiver presente
                    Stock = store != null
                        ? p.StoreStocks.Where(s => s.Store == store).Select(s => (int?)s.Quantity).FirstOrDefault() ?? 0
                        : 0,

                    StoreStocks = p.StoreStocks
                        .GroupBy(s => s.Store)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Quantity).FirstOrDefault()),

                    Visibilities = p.Visibilities != null && p.Visibilities.Any()
                        ? p.Visibilities.GroupBy(v => v.Store)
                            .ToDictionary(g => g.Key, g => g.Select(x => x.IsVisible).FirstOrDefault())
                        : null,

                    CategoryId = p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null,
                    SubcategoryId = p.SubcategoryId,
                    SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null,

                    SortRank = p.SortRank,
                    PinnedTop = p.PinnedTop
                })
                .ToList();
        }
    }
}
