using Imotek.Domain.Properties;
using Imotek.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Persistence;

public sealed class ImotekDbContext(DbContextOptions<ImotekDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Property> Properties => Set<Property>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity tables first, so our configurations can refine them.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImotekDbContext).Assembly);
    }
}
