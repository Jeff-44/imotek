using Imotek.Application.Common;
using Imotek.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[Route("api/admin/properties")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminPropertiesController(PropertyService properties) : ApiControllerBase
{
    /// <summary>All properties, all owners; filter by status with ?status=.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PropertyResponse>>> Search(
        [FromQuery] PropertySearchRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.SearchAllAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PropertyResponse>> Update(Guid id, PropertyRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrProblem(await properties.DeleteAsync(id, cancellationToken));
}
