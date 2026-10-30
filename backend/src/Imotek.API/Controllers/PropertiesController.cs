using Imotek.API.Configuration;
using Imotek.Application.Common;
using Imotek.Application.Images;
using Imotek.Application.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    // Multipart overhead on top of the image limit; larger bodies are cut off with 413.
    private const long MaxUploadRequestBytes = ImageRules.MaxUploadBytes + 64 * 1024;

    /// <summary>Uploads one photo (multipart field "file"): JPEG, PNG or WebP, 5 MB at most.</summary>
    [HttpPost("{id:guid}/images")]
    [Authorize(Roles = $"{Roles.Agent},{Roles.Admin}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadRequestBytes)]
    [EnableRateLimiting(ApiSecurity.UploadRateLimit)]
    public async Task<ActionResult<PropertyResponse>> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await properties.AddImageAsync(
            id, new UploadedFile(content, file.Length, file.FileName, file.ContentType), cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id }, result.Value)
            : ToProblem(result.Error!);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [Authorize(Roles = $"{Roles.Agent},{Roles.Admin}")]
    public async Task<ActionResult<PropertyResponse>> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken) =>
        OkOrProblem(await properties.DeleteImageAsync(id, imageId, cancellationToken));
}
