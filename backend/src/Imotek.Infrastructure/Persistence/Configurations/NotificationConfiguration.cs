using Imotek.Domain.Notifications;
using Imotek.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Imotek.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    private const int EnumMaxLength = 32;

    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(n => n.RequestType).HasConversion<string>().HasMaxLength(EnumMaxLength);
        builder.Property(n => n.PropertyTitle).HasMaxLength(Notification.TitleMaxLength).IsRequired();
        builder.Ignore(n => n.IsRead);

        // RequestId and PropertyId are references for the interface, not foreign keys: a notice is a record of the past.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
    }
}
