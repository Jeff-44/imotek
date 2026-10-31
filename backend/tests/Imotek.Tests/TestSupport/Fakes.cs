using Imotek.Application.Common;
using Imotek.Application.Images;
using Imotek.Application.Properties;
using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Properties;
using Imotek.Domain.Requests;

namespace Imotek.Tests.TestSupport;

internal sealed class InMemoryFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public Task SaveAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        Files[key] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }

    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Files.TryGetValue(key, out var bytes)
            ? new StoredFile(new MemoryStream(bytes), "application/octet-stream", bytes.Length)
            : null);

    public string GetPublicUrl(string key) => $"/media/{key}";
}

internal sealed class FakeCurrentUser(Guid? userId, params string[] roles) : ICurrentUser
{
    public static FakeCurrentUser Anonymous => new(null);

    public static FakeCurrentUser Agent(Guid id) => new(id, Roles.Agent);

    public static FakeCurrentUser Admin(Guid id) => new(id, Roles.Admin);

    public static FakeCurrentUser Client(Guid id) => new(id, Roles.Client);

    public Guid? UserId { get; } = userId;

    public bool IsInRole(string role) => UserId is not null && roles.Contains(role);
}

/// <summary>Approval status of agents, as the real implementation would read it from the account store.</summary>
internal sealed class FakeAgentAccess(bool approvedByDefault = true) : IAgentAccess
{
    /// <summary>Agents that hold the Agent role but are not approved (legacy or pending).</summary>
    public HashSet<Guid> NotApproved { get; } = [];

    public Task<bool> CanPublishAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(approvedByDefault && !NotApproved.Contains(userId));
}

/// <summary>Runs the real filter (<see cref="PropertyQueryExtensions"/>) over an in-memory list.</summary>
internal sealed class InMemoryPropertyRepository : IPropertyRepository
{
    public List<Property> Items { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyDictionary<Guid, Property>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, Property>>(Items.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id));

    public Task<PagedResult<Property>> SearchAsync(PropertyFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Items.AsQueryable().ApplyFilter(filter);
        var items = query.OrderByNewest().Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<Property>(items, page, pageSize, query.Count()));
    }

    public void Add(Property property) => Items.Add(property);

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

internal sealed class InMemoryUserDirectory : IUserDirectory
{
    public Dictionary<Guid, UserSummary> Users { get; } = [];

    public UserSummary Add(Guid id, string? phone = null, bool consent = false, bool approvedAgent = false) =>
        Users[id] = new UserSummary(id, $"User {id.ToString()[..4]}", $"{id:N}@example.test", phone, consent, approvedAgent);

    public Task<IReadOnlyDictionary<Guid, UserSummary>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UserSummary>>(Users.Where(u => ids.Contains(u.Key)).ToDictionary(u => u.Key, u => u.Value));
}

/// <summary>Requests and notifications in memory; <see cref="SaveChangesAsync"/> applies the one-active-request rule like the unique index.</summary>
internal sealed class InMemoryRequestStore : IClientRequestRepository, INotificationRepository
{
    private readonly List<ClientRequest> _pending = [];

    public List<ClientRequest> Requests { get; } = [];

    public List<Notification> Notifications { get; } = [];

    public Task<ClientRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<ClientRequest>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ClientRequest>>(Requests.Where(r => r.ClientId == clientId).OrderByDescending(r => r.CreatedAt).ToList());

    public Task<IReadOnlyList<ClientRequest>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ClientRequest>>(Requests.Where(r => r.OwnerId == ownerId).OrderByDescending(r => r.CreatedAt).ToList());

    public Task<bool> HasActiveAsync(RequestType type, Guid propertyId, Guid clientId, CancellationToken cancellationToken) =>
        Task.FromResult(Requests.Any(r => r.Type == type && r.PropertyId == propertyId && r.ClientId == clientId && r.IsActive));

    public Task<bool> HasActiveGuestAsync(RequestType type, Guid propertyId, string email, CancellationToken cancellationToken) =>
        Task.FromResult(Requests.Any(r => r.Type == type && r.PropertyId == propertyId && r.IsGuest
            && string.Equals(r.GuestEmail, email, StringComparison.OrdinalIgnoreCase) && r.IsActive));

    public Task<ClientRequest?> GetByGuestTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.GuestAccessTokenHash == tokenHash));

    public void Add(ClientRequest request) => _pending.Add(request);

    public void AddNotification(Notification notification) => Notifications.Add(notification);

    public Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        Requests.AddRange(_pending);
        _pending.Clear();
        return Task.FromResult(true);
    }

    Task<IReadOnlyList<Notification>> INotificationRepository.ListAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(take).ToList());

    Task<int> INotificationRepository.CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.Count(n => n.UserId == userId && !n.IsRead));

    Task<Notification?> INotificationRepository.GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Notifications.FirstOrDefault(n => n.Id == id));

    Task<int> INotificationRepository.MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var unread = Notifications.Where(n => n.UserId == userId && !n.IsRead).ToList();
        unread.ForEach(n => n.MarkRead(utcNow));
        return Task.FromResult(unread.Count);
    }

    Task INotificationRepository.SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
