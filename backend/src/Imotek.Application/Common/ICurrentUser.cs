namespace Imotek.Application.Common;

/// <summary>The caller of the current request, as established by authentication.</summary>
public interface ICurrentUser
{
    /// <summary>Null for anonymous callers.</summary>
    Guid? UserId { get; }

    bool IsInRole(string role);
}
