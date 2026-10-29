using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Persistence;

public sealed class ImotekDbContext(DbContextOptions<ImotekDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImotekDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
