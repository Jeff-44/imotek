using Imotek.Application.Images;
using Microsoft.Extensions.Options;

namespace Imotek.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Directory for uploaded files. In containers, mount a persistent volume here.</summary>
    public string RootPath { get; set; } = "data/uploads";

    /// <summary>URL prefix under which the API serves the files.</summary>
    public string PublicBasePath { get; set; } = "/media";
}

/// <summary>
/// Stores uploads on the local disk. Suitable for a single server with a persistent volume;
/// for several instances or a CDN, replace with an S3-compatible implementation of <see cref="IFileStorage"/>.
/// </summary>
public sealed class LocalFileStorage(IOptions<FileStorageOptions> options, string contentRoot) : IFileStorage
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
    };

    // A relative RootPath is resolved against the application's content root, not the working directory.
    private readonly string _root = Path.GetFullPath(Path.Combine(contentRoot, options.Value.RootPath));
    private readonly string _publicBasePath = options.Value.PublicBasePath.TrimEnd('/');

    public async Task SaveAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        var path = Resolve(key) ?? throw new ArgumentException("Invalid storage key.", nameof(key));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        if (Resolve(key) is { } path && File.Exists(path))
        {
            File.Delete(path);

            // Remove the per-property folder once its last photo is gone.
            var folder = Path.GetDirectoryName(path)!;
            if (folder != _root && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }

        return Task.CompletedTask;
    }

    public Task<StoredFile?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        if (Resolve(key) is not { } path
            || !File.Exists(path)
            || !ContentTypes.TryGetValue(Path.GetExtension(path), out var contentType))
        {
            return Task.FromResult<StoredFile?>(null);
        }

        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult<StoredFile?>(new StoredFile(stream, contentType, stream.Length));
    }

    public string GetPublicUrl(string key) => $"{_publicBasePath}/{key}";

    /// <summary>Maps a key to a path inside the root, rejecting anything that escapes it.</summary>
    private string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('\\') || key.Contains(':') || Path.IsPathRooted(key))
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? path : null;
    }
}
