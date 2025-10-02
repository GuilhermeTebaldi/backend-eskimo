// CSharpAssistant.API/Services/ProductService.cs
using System;
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

        /// <summary>
        /// Lista produtos com filtro por nome, paginação e visão opcional por loja.
        /// - Front (loja): quando "store" vier e adminMode=false, filtra por Quantity > 0.
        /// - Admin: quando adminMode=true, não filtra por quantidade (mostra todos).
        /// Sempre retorna:
        /// - Stock: quantidade da loja quando "store" vier (compat).
        /// - StoreStocks: dicionário completo por loja.
        /// - Visibilities: dicionário por loja.
        /// </summary>
        public IList<ProductDTO> GetAllProducts(string? name, int page, int pageSize, string? store, bool adminMode = false)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Subcategory)
                .Include(p => p.StoreStocks)
                .Include(p => p.Visibilities)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                var term = name.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term) ||
                    (p.Description != null && p.Description.Contains(term)));
            }

            if (!string.IsNullOrEmpty(store))
            {
                if (!adminMode)
                {
                    // Loja pública: só produtos com estoque > 0 na loja.
                    query = query.Where(p => p.StoreStocks.Any(s => s.Store == store && s.Quantity > 0));
                }
                else
                {
                    // Admin: apenas exige registro da loja, mesmo com 0.
                    query = query.Where(p => p.StoreStocks.Any(s => s.Store == store));
                }
            }

            var items = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDTO
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,

                    // Compat: quando "store" vier, popula Stock com a quantidade da loja.
                    Stock = store != null
                        ? p.StoreStocks
                            .Where(s => s.Store == store)
                            .Select(s => (int?)s.Quantity)
                            .FirstOrDefault() ?? 0
                        : 0,

                    StoreStocks = p.StoreStocks
                        .GroupBy(s => s.Store)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.Quantity).FirstOrDefault()),

                    Visibilities = p.Visibilities != null && p.Visibilities.Any()
                        ? p.Visibilities
                            .GroupBy(v => v.Store)
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

            return items;
        }
    }
}
