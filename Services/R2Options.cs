namespace scale_api_poc.Services;

/// <summary>
/// Configuration for Cloudflare R2 (S3-compatible) storage.
/// Create API tokens at https://dash.cloudflare.com → R2 → Manage R2 API Tokens.
/// </summary>
public class R2Options
{
    public const string SectionName = "R2";

    /// <summary>Cloudflare account ID (found in R2 dashboard URL or account settings).</summary>
    public string AccountId { get; set; } = "";

    /// <summary>R2 API token Access Key ID.</summary>
    public string AccessKeyId { get; set; } = "";

    /// <summary>R2 API token Secret Access Key.</summary>
    public string SecretAccessKey { get; set; } = "";

    /// <summary>Default bucket name for storage operations.</summary>
    public string BucketName { get; set; } = "";

    /// <summary>R2 endpoint. Defaults to https://&lt;AccountId&gt;.r2.cloudflarestorage.com when empty.</summary>
    public string? ServiceUrl => string.IsNullOrWhiteSpace(AccountId)
        ? null
        : $"https://{AccountId.Trim()}.r2.cloudflarestorage.com";
}
