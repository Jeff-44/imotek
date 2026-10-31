using Imotek.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(NotificationService notifications) : ApiControllerBase
{
    /// <summary>The latest notifications of the caller, with the number still unread.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificationList>> List(CancellationToken cancellationToken) =>
        OkOrProblem(await notifications.ListAsync(cancellationToken));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid id, CancellationToken cancellationToken) =>
        NoContentOrProblem(await notifications.MarkReadAsync(id, cancellationToken));

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead(CancellationToken cancellationToken) =>
        NoContentOrProblem(await notifications.MarkAllReadAsync(cancellationToken));
}
