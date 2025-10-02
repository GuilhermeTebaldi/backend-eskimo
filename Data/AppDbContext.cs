// CSharpAssistant.API/Data/AppDbContext.cs
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
        public DbSet<PaymentConfig> PaymentConfigs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 🔒 Garantir único ProductId+Store em estoques
            modelBuilder.Entity<StoreStock>()
                .HasIndex(s => new { s.ProductId, s.Store })
                .IsUnique();

            // 🔒 Garantir único ProductId+Store em visibilidade
            modelBuilder.Entity<StoreProductVisibility>()
                .HasIndex(v => new { v.ProductId, v.Store })
                .IsUnique();

            // 🔒 Uma config por loja
            modelBuilder.Entity<PaymentConfig>()
                .HasIndex(p => p.Store)
                .IsUnique();
        }
    }
}
