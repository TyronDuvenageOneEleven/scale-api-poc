namespace scale_api_poc.Services;

/// <summary>
/// Abstraction for Cloudflare R2 object storage (S3-compatible).
/// </summary>
public interface IR2StorageService
{
    /// <summary>Upload a file from a stream. Uses default bucket from config.</summary>
    Task<string> UploadAsync(string key, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>Download an object as a stream. Uses default bucket from config.</summary>
    Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Get object stream and metadata (e.g. content type) for streaming responses.</summary>
    Task<R2ObjectResult> GetWithMetadataAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>List objects in the bucket with an optional prefix.</summary>
    Task<IReadOnlyList<R2ObjectInfo>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default);

    /// <summary>Delete an object. Uses default bucket from config.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Generate a presigned GET URL for temporary public access.</summary>
    string GetPresignedDownloadUrl(string key, TimeSpan validity);

    /// <summary>Check if R2 is configured (credentials and bucket set).</summary>
    bool IsConfigured { get; }
}

/// <summary>Metadata for an object in R2.</summary>
public record R2ObjectInfo(string Key, long Size, DateTime LastModified);

/// <summary>Object stream plus metadata for streaming responses.</summary>
public class R2ObjectResult : IDisposable
{
    public Stream Stream { get; init; } = null!;
    public string? ContentType { get; init; }
    public long ContentLength { get; init; }
    public void Dispose() => Stream?.Dispose();
}
