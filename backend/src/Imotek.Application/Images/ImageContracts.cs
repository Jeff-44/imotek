namespace Imotek.Application.Images;

public static class ImageRules
{
    public const long MaxUploadBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
}

/// <summary>A file received from the client. Its name and content type are not trusted.</summary>
public sealed record UploadedFile(Stream Content, long Length, string? FileName, string? ContentType);

/// <summary>An image whose real format was checked from its bytes and whose metadata (EXIF, GPS…) was removed.</summary>
public sealed record SanitizedImage(byte[] Content, string ContentType, string Extension);

public interface IImageProcessor
{
    /// <summary>Null when the bytes are not a supported, well-formed image.</summary>
    SanitizedImage? Sanitize(byte[] content);
}

public sealed record StoredFile(Stream Content, string ContentType, long Length);

/// <summary>
/// Where uploaded files live. The local-disk implementation is for a single server;
/// an S3-compatible implementation can replace it without touching the use cases.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    /// <summary>Null when the file does not exist.</summary>
    Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>URL the browser uses to display the file.</summary>
    string GetPublicUrl(string key);
}
