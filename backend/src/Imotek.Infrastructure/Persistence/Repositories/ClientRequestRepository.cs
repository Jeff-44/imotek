using Imotek.Application.Requests;
using Imotek.Domain.Notifications;
using Imotek.Domain.Requests;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Imotek.Infrastructure.Persistence.Repositories;

internal sealed class ClientRequestRepository(ImotekDbContext db) : IClientRequestRepository
{
    public Task<ClientRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ClientRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ClientRequest>> ListByClientAsync(Guid clientId, CancellationToken cancellationToken) =>
        await db.ClientRequests.AsNoTracking()
            .Where(r => r.ClientId == clientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClientRequest>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.ClientRequests.AsNoTracking()
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> HasActiveAsync(RequestType type, Guid propertyId, Guid clientId, CancellationToken cancellationToken)
    {
        IQueryable<ClientRequest> set = type switch
        {
            RequestType.Rental => db.Set<RentalRequest>(),
            RequestType.Purchase => db.Set<PurchaseRequest>(),
            _ => db.Set<VisitRequest>(),
        };
        var active = ClientRequest.ActiveStatuses.ToList();
        return set.AnyAsync(r => r.PropertyId == propertyId && r.ClientId == clientId && active.Contains(r.Status), cancellationToken);
    }

    public void Add(ClientRequest request) => db.ClientRequests.Add(request);

    public void AddNotification(Notification notification) => db.Notifications.Add(notification);

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two identical requests raced past the HasActiveAsync check: the index kept only one.
            db.ChangeTracker.Clear();
            return false;
        }
    }
}

internal sealed class NotificationRepository(ImotekDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> ListAsync(Guid userId, int take, CancellationToken cancellationToken) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<int> MarkAllReadAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken) =>
        db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, utcNow), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
