using Microsoft.AspNetCore.Mvc;
using scale_api_poc.Services;

namespace scale_api_poc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StorageController : ControllerBase
{
    private readonly IR2StorageService _storage;

    public StorageController(IR2StorageService storage)
    {
        _storage = storage;
    }

    /// <summary>
    /// Check if R2 storage is configured and available.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new { configured = _storage.IsConfigured });
    }

    /// <summary>
    /// Upload a file to R2. Key can include path segments (e.g. "uploads/file.txt").
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Upload([FromQuery] string key, IFormFile file, CancellationToken cancellationToken)
    {
        if (!_storage.IsConfigured)
            return StatusCode(503, "R2 storage is not configured.");
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("Query parameter 'key' is required.");
        if (file == null || file.Length == 0)
            return BadRequest("No file or empty file.");

        await using var stream = file.OpenReadStream();
        var etag = await _storage.UploadAsync(key, stream, file.ContentType, cancellationToken);
        return Ok(new { key, etag });
    }

    /// <summary>
    /// Get a temporary presigned URL to download an object (valid 1 hour).
    /// </summary>
    [HttpGet("presigned-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetPresignedUrl([FromQuery] string key)
    {
        if (!_storage.IsConfigured)
            return StatusCode(503, "R2 storage is not configured.");
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("Query parameter 'key' is required.");

        var url = _storage.GetPresignedDownloadUrl(key, TimeSpan.FromHours(1));
        return Ok(new { url });
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp", ".ico" };

    /// <summary>
    /// List images in the R2 bucket. Optional prefix to scope to a folder (e.g. "uploads/").
    /// Only objects with common image extensions are returned unless imageOnly is false.
    /// </summary>
    [HttpGet("images")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListImages(
        [FromQuery] string? prefix = null,
        [FromQuery] bool imageOnly = true,
        CancellationToken cancellationToken = default)
    {
        if (!_storage.IsConfigured)
            return StatusCode(503, "R2 storage is not configured.");

        var objects = await _storage.ListAsync(prefix, cancellationToken);
        var items = objects.AsEnumerable();
        if (imageOnly)
            items = items.Where(o => ImageExtensions.Contains(Path.GetExtension(o.Key)));

        var result = items.Select(o => new
        {
            o.Key,
            o.Size,
            LastModified = o.LastModified,
            Url = _storage.GetPresignedDownloadUrl(o.Key, TimeSpan.FromHours(1))
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Stream an image from R2 by key. Key can include path segments (e.g. "uploads/photo.png").
    /// </summary>
    [HttpGet("images/{*key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetImage(string key, CancellationToken cancellationToken = default)
    {
        if (!_storage.IsConfigured)
            return StatusCode(503, "R2 storage is not configured.");
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("Key is required.");

        try
        {
            var result = await _storage.GetWithMetadataAsync(key, cancellationToken);
            var contentType = result.ContentType ?? "application/octet-stream";
            return File(result.Stream, contentType);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }
}
