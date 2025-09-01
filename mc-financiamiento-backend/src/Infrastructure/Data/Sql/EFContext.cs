using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Sql;

public class EFContext : DbContext
{
    public EFContext(DbContextOptions<EFContext> options) : base(options) { }

}
