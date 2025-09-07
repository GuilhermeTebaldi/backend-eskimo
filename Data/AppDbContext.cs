//CSharpAssistant.API/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Models;

namespace CSharpAssistant.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Subcategory> Subcategories { get; set; }

        public DbSet<User> Users { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderItem> OrderItems { get; set; }

        public DbSet<StoreStock> StoreStocks { get; set; }

        public DbSet<StoreProductVisibility> StoreProductVisibilities { get; set; }

        public DbSet<Setting> Settings { get; set; }

        // ✅ NOVO: Configurações de pagamento por loja (CNPJ/credenciais)
        public DbSet<PaymentConfig> PaymentConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔒 Opcional: índice único por Store (uma config por loja)
            modelBuilder.Entity<PaymentConfig>()
                .HasIndex(p => p.Store)
                .IsUnique();
        }
    }
}
