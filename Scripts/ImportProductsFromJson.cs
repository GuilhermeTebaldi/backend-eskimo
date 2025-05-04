using System.Text.Json;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Scripts;

public static class ImportProductsFromJson
{
    public static void Run(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var path = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", "produtos_backup.json");

        if (!File.Exists(path))
        {
            Console.WriteLine($"❌ Arquivo JSON não encontrado em: {path}");
            return;
        }

        var json = File.ReadAllText(path);
        var products = JsonSerializer.Deserialize<List<JsonProduct>>(json);

        if (products == null || products.Count == 0)
        {
            Console.WriteLine("⚠️ Nenhum produto encontrado no JSON.");
            return;
        }

        foreach (var p in products)
        {
            if (db.Products.Any(x => x.Name == p.name))
                continue;

            // ⚠️ Validação de categoria
            if (!db.Categories.Any(c => c.Id == p.categoryId))
            {
                Console.WriteLine($"❌ Categoria inválida: {p.categoryId}, ignorando produto: {p.name}");
                continue;
            }

            // ⚠️ Validação de subcategoria
            if (p.subcategoryId != null && !db.Subcategories.Any(s => s.Id == p.subcategoryId))
            {
                Console.WriteLine($"❌ Subcategoria inválida: {p.subcategoryId}, ignorando produto: {p.name}");
                continue;
            }

            var product = new Product
            {
                Name = p.name,
                Description = p.description,
                Price = p.price,
                ImageUrl = p.imageUrl,
                CategoryId = p.categoryId,
                SubcategoryId = p.subcategoryId
            };

            db.Products.Add(product);
            db.SaveChanges(); // salvar para gerar ID

            foreach (var store in new[] { "Efapi", "Palmital", "Passo dos Fortes" })
            {
                db.StoreStocks.Add(new StoreStock
                {
                    ProductId = product.Id,
                    Store = store,
                    Quantity = p.stock
                });
            }

            Console.WriteLine($"✅ Produto adicionado: {product.Name}");
        }

        db.SaveChanges();
        Console.WriteLine("✅ Importação finalizada!");
    }

    private class JsonProduct
    {
        public string name { get; set; }
        public string description { get; set; }
        public decimal price { get; set; }
        public string imageUrl { get; set; }
        public int stock { get; set; }
        public int categoryId { get; set; }
        public int? subcategoryId { get; set; }
    }
}
