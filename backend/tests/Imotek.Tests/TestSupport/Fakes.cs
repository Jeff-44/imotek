using Imotek.Application.Common;
using Imotek.Application.Properties;
using Imotek.Domain.Properties;

namespace Imotek.Tests.TestSupport;

internal sealed class FakeCurrentUser(Guid? userId, params string[] roles) : ICurrentUser
{
    public static FakeCurrentUser Anonymous => new(null);

    public static FakeCurrentUser Agent(Guid id) => new(id, Roles.Agent);

    public static FakeCurrentUser Admin(Guid id) => new(id, Roles.Admin);

    public static FakeCurrentUser Client(Guid id) => new(id, Roles.Client);

    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => UserId is not null && roles.Contains(role);
}

/// <summary>Runs the real filter (<see cref="PropertyQueryExtensions"/>) over an in-memory list.</summary>
internal sealed class InMemoryPropertyRepository : IPropertyRepository
{
    public List<Property> Items { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<PagedResult<Property>> SearchAsync(PropertyFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Items.AsQueryable().ApplyFilter(filter);
        var items = query.OrderByNewest().Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<Property>(items, page, pageSize, query.Count()));
    }

    public void Add(Property property) => Items.Add(property);

    public void Remove(Property property) => Items.Remove(property);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}
