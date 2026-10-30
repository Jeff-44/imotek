using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace Imotek.Infrastructure.Persistence.Repositories;

internal sealed class PropertyRepository(ImotekDbContext db) : IPropertyRepository
{
    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Properties.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PagedResult<Property>> SearchAsync(PropertyFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Properties.AsNoTracking().ApplyFilter(filter);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByNewest()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Property>(items, page, pageSize, total);
    }

    public void Add(Property property) => db.Properties.Add(property);

    public void Remove(Property property) => db.Properties.Remove(property);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
