using Microsoft.AspNetCore.Identity;

namespace Imotek.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public const int FullNameMaxLength = 150;

    public string FullName { get; set; } = string.Empty;
}
