using System.Text.RegularExpressions;
using Imotek.Application.Images;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Imotek.API.Controllers;

/// <summary>Serves uploaded photos from the local file storage.</summary>
[ApiController]
[Route("media")]
[AllowAnonymous]
public sealed partial class MediaController(IFileStorage storage) : ControllerBase
{
    // Only keys the server itself generates: properties/{propertyId}/{imageId}.{ext}
    [GeneratedRegex("^properties/[0-9a-f]{32}/[0-9a-f]{32}\\.(jpg|png|webp)$")]
    private static partial Regex StorageKey();

    [HttpGet("{**key}")]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken)
    {
        if (!StorageKey().IsMatch(key) || await storage.OpenReadAsync(key, cancellationToken) is not { } file)
        {
            return NotFound();
        }

        // Keys are unique per upload, so the content never changes.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(file.Content, file.ContentType);
    }
}
