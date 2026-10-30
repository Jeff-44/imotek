using Imotek.Application.Common;
using Imotek.Domain.Properties;

namespace Imotek.Application.Properties;

public interface IPropertyRepository
{
    /// <summary>Returns a tracked entity, or null.</summary>
    Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Applies <see cref="PropertyQueryExtensions.ApplyFilter"/>, newest first.</summary>
    Task<PagedResult<Property>> SearchAsync(PropertyFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    void Add(Property property);

    void Remove(Property property);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
