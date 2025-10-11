// CSharpAssistant.API/Services/ProductService.cs  (inalterado no comportamento + ordenação do layout)
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

        public ProductService(AppDbContext context)
        {
            _context = context;
        }

        // Sem store => lista todos. Com store => só com estoque > 0 nessa loja.
        public IEnumerable<ProductDTO> GetAllProducts(string? nameFilter = null, int page = 1, int pageSize = 10, string? store = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .AsQueryable();

            if (!string.IsNullOrEmpty(store))
            {
                query = query.Where(p =>
                    p.StoreStocks != null &&
                    p.StoreStocks.Any(s => s.Store == store && s.Quantity > 0)
                );
            }

            if (!string.IsNullOrEmpty(nameFilter))
            {
                var f = nameFilter.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(f));
            }

            // Ordenação para refletir o layout salvo
            query = query
                .OrderByDescending(p => p.PinnedTop ?? false)
                .ThenBy(p => p.SortRank ?? int.MaxValue)
                .ThenBy(p => p.Name);

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
                    Stock = store != null
                        ? (p.StoreStocks != null
                            ? p.StoreStocks.Where(s => s.Store == store).Select(s => s.Quantity).FirstOrDefault()
                            : 0)
                        : 0,
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
