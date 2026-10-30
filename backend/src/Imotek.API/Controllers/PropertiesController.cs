using Imotek.Application.Common;
using Imotek.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

[Route("api/properties")]
public sealed class PropertiesController(PropertyService properties) : ApiControllerBase
{
    /// <summary>Public catalogue: available properties only.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<PropertyResponse>>> Search(
        [FromQuery] PropertySearchRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.SearchPublicAsync(request, cancellationToken));

    /// <summary>Available properties for everyone; owners and admins also see the other statuses.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PropertyResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.GetByIdAsync(id, cancellationToken));

    [HttpGet("my-properties")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<PagedResult<PropertyResponse>>> MyProperties(
        [FromQuery] PropertySearchRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.SearchMineAsync(request, cancellationToken));

    [HttpPost]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<PropertyResponse>> Create(PropertyRequest request, CancellationToken cancellationToken)
    {
        var result = await properties.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : ToProblem(result.Error!);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<PropertyResponse>> Update(Guid id, PropertyRequest request, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        NoContentOrProblem(await properties.DeleteAsync(id, cancellationToken));
}
