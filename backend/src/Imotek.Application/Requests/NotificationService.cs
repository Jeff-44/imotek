using Imotek.Application.Common;

namespace Imotek.Application.Requests;

/// <summary>In-app notifications. Each user only ever reads and marks their own.</summary>
public sealed class NotificationService(INotificationRepository notifications, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public const int MaxListed = 50;

    public async Task<Result<NotificationList>> ListAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var items = await notifications.ListAsync(userId, MaxListed, cancellationToken);
        var unread = await notifications.CountUnreadAsync(userId, cancellationToken);
        return new NotificationList(
            items.Select(n => new NotificationResponse(
                n.Id, n.Kind, n.RequestId, n.RequestType, n.PropertyId, n.PropertyTitle, n.CreatedAt, n.IsRead)).ToList(),
            unread);
    }

    public async Task<Result> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        var notification = await notifications.GetByIdAsync(id, cancellationToken);
        if (notification is null || notification.UserId != userId)
        {
            return Error.NotFound("The notification was not found.");
        }

        notification.MarkRead(timeProvider.GetUtcNow().UtcDateTime);
        await notifications.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized();
        }

        await notifications.MarkAllReadAsync(userId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return Result.Success();
    }
}
