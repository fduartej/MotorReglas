using Microsoft.EntityFrameworkCore;
using Contoso.Modules.eComerce.Entities;

namespace Infrastructure.Data.NoSql;

public class CosmosContext : DbContext
{
    public CosmosContext(DbContextOptions<CosmosContext> options) : base(options) { }

    public DbSet<Product> DbSetProducts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>()
            .ToContainer("Product")
            .HasNoDiscriminator()
             .Property(e => e.Id)
            .ToJsonProperty("id");
    }
}
