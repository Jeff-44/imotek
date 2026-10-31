using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Persistence;

public sealed class ImotekDbContext(DbContextOptions<ImotekDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Property> Properties => Set<Property>();

    /// <summary>Rental, purchase and visit requests: one table each (TPC), queried together through this set.</summary>
    public DbSet<ClientRequest> ClientRequests => Set<ClientRequest>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity tables first, so our configurations can refine them.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ImotekDbContext).Assembly);
    }
}
