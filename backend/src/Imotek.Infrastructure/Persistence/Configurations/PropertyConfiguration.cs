using Imotek.Domain.Properties;
using Imotek.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Imotek.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    private const int EnumMaxLength = 32;

    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Title).HasMaxLength(Property.TitleMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Property.DescriptionMaxLength).IsRequired();
        builder.Property(p => p.Country).HasMaxLength(Property.LocationMaxLength).IsRequired();
        builder.Property(p => p.Department).HasMaxLength(Property.LocationMaxLength).IsRequired();
        builder.Property(p => p.City).HasMaxLength(Property.LocationMaxLength).IsRequired();
        builder.Property(p => p.Address).HasMaxLength(Property.AddressMaxLength).IsRequired();

        // Enums as text: readable in SQL and safe if members are reordered.
        builder.Property(p => p.PropertyType).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(p => p.TransactionType).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(p => p.Currency).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(EnumMaxLength);

        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.Property(p => p.Area).HasPrecision(12, 2);

        builder.PrimitiveCollection(p => p.ImageUrls)
            .HasField("_imageUrls")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired();

        builder.Ignore(p => p.IsPubliclyVisible);
        builder.Ignore(p => p.PhotoCount);
        builder.Ignore(p => p.CanAddImage);
        builder.Ignore(p => p.NextImageSortOrder);

        // Normalized copies used by the search filters (lower-case, no accents).
        builder.Property(p => p.SearchKey).IsRequired();
        builder.Property(p => p.LocationKey).HasMaxLength(Property.LocationMaxLength * 2 + Property.AddressMaxLength + 2).IsRequired();
        builder.Property(p => p.DepartmentKey).HasMaxLength(Property.LocationMaxLength).IsRequired();
        builder.HasIndex(p => p.DepartmentKey);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey("PropertyId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Images)
            .HasField("_images")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();

        // The domain only knows OwnerId; the relationship to the user table is an infrastructure concern.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.OwnerId);
        builder.HasIndex(p => new { p.Status, p.CreatedAt });
    }
}
