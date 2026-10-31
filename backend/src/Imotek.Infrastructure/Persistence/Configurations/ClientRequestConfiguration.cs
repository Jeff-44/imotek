using Imotek.Domain.Properties;
using Imotek.Domain.Requests;
using Imotek.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Imotek.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table-per-concrete-type: RentalRequests, PurchaseRequests and VisitRequests are separate tables
/// with the shared columns repeated; <see cref="ImotekDbContext.ClientRequests"/> queries them together.
/// </summary>
internal sealed class ClientRequestConfiguration : IEntityTypeConfiguration<ClientRequest>
{
    public const int EnumMaxLength = 16;

    public void Configure(EntityTypeBuilder<ClientRequest> builder)
    {
        builder.UseTpcMappingStrategy();
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(r => r.Message).HasMaxLength(ClientRequest.MessageMaxLength);
        builder.Property(r => r.AgentResponse).HasMaxLength(ClientRequest.MessageMaxLength);
        builder.Ignore(r => r.Type);
        builder.Ignore(r => r.IsActive);
        builder.Ignore(r => r.IsGuest);

        // Visitors without an account (ClientId null): the contact details they typed in the form.
        builder.Property(r => r.GuestName).HasMaxLength(GuestContact.FullNameMaxLength);
        builder.Property(r => r.GuestEmail).HasMaxLength(GuestContact.EmailMaxLength);
        builder.Property(r => r.GuestPhone).HasMaxLength(GuestContact.PhoneMaxLength);
        builder.Property(r => r.GuestAccessTokenHash).HasMaxLength(64);

        builder.HasOne<Property>().WithMany().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ClientId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>One active request per client (or visitor email), property and kind; unique follow-up links.</summary>
    public static void ConfigureTable<T>(EntityTypeBuilder<T> builder, string table)
        where T : ClientRequest
    {
        builder.ToTable(table);

        var active = string.Join(", ", ClientRequest.ActiveStatuses.Select(s => $"'{s}'"));
        builder.HasIndex(r => new { r.PropertyId, r.ClientId })
            .IsUnique()
            .HasFilter($"\"Status\" IN ({active})")
            .HasDatabaseName($"IX_{table}_Active_PropertyId_ClientId");

        // Visitors: one open request per email, property and kind.
        builder.HasIndex(r => new { r.PropertyId, r.GuestEmail })
            .IsUnique()
            .HasFilter($"\"ClientId\" IS NULL AND \"Status\" IN ({active})")
            .HasDatabaseName($"IX_{table}_ActiveGuest_PropertyId_GuestEmail");

        builder.HasIndex(r => r.GuestAccessTokenHash)
            .IsUnique()
            .HasFilter("\"GuestAccessTokenHash\" IS NOT NULL")
            .HasDatabaseName($"IX_{table}_GuestAccessTokenHash");
    }
}

internal sealed class RentalRequestConfiguration : IEntityTypeConfiguration<RentalRequest>
{
    public void Configure(EntityTypeBuilder<RentalRequest> builder)
    {
        ClientRequestConfiguration.ConfigureTable(builder, "RentalRequests");
        builder.Property(r => r.CurrentAddress).HasMaxLength(RentalDetails.CurrentAddressMaxLength);
    }
}

internal sealed class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
{
    public void Configure(EntityTypeBuilder<PurchaseRequest> builder) =>
        ClientRequestConfiguration.ConfigureTable(builder, "PurchaseRequests");
}

internal sealed class VisitRequestConfiguration : IEntityTypeConfiguration<VisitRequest>
{
    public void Configure(EntityTypeBuilder<VisitRequest> builder) =>
        ClientRequestConfiguration.ConfigureTable(builder, "VisitRequests");
}
