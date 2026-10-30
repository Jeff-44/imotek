using Imotek.Application.Common;
using Imotek.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Imotek.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder) =>
        builder.Property(u => u.FullName).HasMaxLength(ApplicationUser.FullNameMaxLength).IsRequired();
}

/// <summary>Roles are reference data, created by the migration rather than at startup.</summary>
internal sealed class RoleSeedConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    // Fixed values keep the migration stable.
    private static readonly IdentityRole<Guid>[] SeedRoles =
    [
        Role("0199e5a0-0000-7000-8000-000000000001", Roles.Admin),
        Role("0199e5a0-0000-7000-8000-000000000002", Roles.Agent),
        Role("0199e5a0-0000-7000-8000-000000000003", Roles.Client),
    ];

    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) => builder.HasData(SeedRoles);

    private static IdentityRole<Guid> Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
