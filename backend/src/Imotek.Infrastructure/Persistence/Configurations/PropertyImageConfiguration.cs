using Imotek.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Imotek.Infrastructure.Persistence.Configurations;

internal sealed class PropertyImageConfiguration : IEntityTypeConfiguration<PropertyImage>
{
    public void Configure(EntityTypeBuilder<PropertyImage> builder)
    {
        builder.ToTable("PropertyImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.StorageKey).HasMaxLength(PropertyImage.StorageKeyMaxLength).IsRequired();
        builder.HasIndex(i => i.StorageKey).IsUnique();
        builder.Property(i => i.ContentType).HasMaxLength(PropertyImage.ContentTypeMaxLength).IsRequired();
    }
}
