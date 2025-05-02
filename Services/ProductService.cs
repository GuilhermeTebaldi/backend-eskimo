public IEnumerable<ProductDTO> GetAllProducts(string? nameFilter = null, int page = 1, int pageSize = 10, string? store = null)
{
    var query = _context.Products
        .Include(p => p.Category)
        .Include(p => p.Subcategory)
        .Include(p => p.Visibilities)
        .Include(p => p.StoreStocks) // ✅ agora incluímos os estoques
        .AsQueryable();

    // 🔍 Filtro por loja: visibilidade + estoque > 0
    if (!string.IsNullOrEmpty(store))
    {
        query = query.Where(p =>
            p.Visibilities.Any(v => v.Store == store && v.IsVisible) &&
            p.StoreStocks.Any(s => s.Store == store && s.Quantity > 0)
        );
    }

    // 🔍 Filtro por nome
    if (!string.IsNullOrEmpty(nameFilter))
    {
        nameFilter = nameFilter.ToLower();
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
            Stock = p.StoreStocks
                .Where(s => s.Store == store)
                .Select(s => s.Quantity)
                .FirstOrDefault(), // ✅ mostra o estoque da loja atual
            CategoryId = p.CategoryId,
            CategoryName = p.Category != null ? p.Category.Name : null,
            SubcategoryId = p.SubcategoryId,
            SubcategoryName = p.Subcategory != null ? p.Subcategory.Name : null
        })
        .ToList();
}
