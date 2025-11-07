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
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<PaymentConfig> PaymentConfigs { get; set; }
        public DbSet<StoreSetting> StoreSettings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 👤 Users
modelBuilder.Entity<User>()
    .HasIndex(u => u.Email)
    .IsUnique();

modelBuilder.Entity<User>()
    .Property(u => u.Permissions)
    .HasColumnType("jsonb")
    .HasDefaultValueSql("'{}'::jsonb");

modelBuilder.Entity<User>()
    .Property(u => u.IsEnabled)
    .HasDefaultValue(true);


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

            // === Settings: horários de funcionamento ===
            modelBuilder.Entity<Setting>(b =>
            {
                b.Property(s => s.TimeZone)
                 .HasMaxLength(64)
                 .HasDefaultValue("America/Sao_Paulo");

                b.Property(s => s.OpeningHoursJson)
                 .HasColumnType("jsonb")
                 .HasDefaultValueSql("'{}'::jsonb");

                b.Property(s => s.ExceptionsJson)
                 .HasColumnType("jsonb")
                 .HasDefaultValueSql("'[]'::jsonb");
            });

            modelBuilder.Entity<StoreSetting>(b =>
            {
                b.HasIndex(s => s.Store).IsUnique(); // uma config por loja
                b.Property(s => s.TimeZone).HasMaxLength(64).HasDefaultValue("America/Sao_Paulo");
                b.Property(s => s.OpeningHoursJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
                b.Property(s => s.ExceptionsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            });

            modelBuilder.Entity<Promotion>(b =>
            {
                b.Property(p => p.HighlightText).HasMaxLength(160);
                b.Property(p => p.PreviousPrice).HasColumnType("numeric(18,2)");
                b.Property(p => p.CurrentPrice).HasColumnType("numeric(18,2)");
                b.Property(p => p.IsActive).HasDefaultValue(true);
                b.Property(p => p.CreatedAt).HasDefaultValueSql("NOW()");
                b.Property(p => p.UpdatedAt).HasDefaultValueSql("NOW()");
            });
        }
    }
}
