using Microsoft.EntityFrameworkCore;
using e_commerce.Models;
using CSharpAssistant.API.Models;


namespace e_commerce.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Subcategory> Subcategories { get; set; }        // ✅ Adicione essas linhas:
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Setting> Settings { get; set; }
        

    }
}
