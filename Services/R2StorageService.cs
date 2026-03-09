using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace scale_api_poc.Services;

/// <summary>
/// Cloudflare R2 storage implementation using the S3-compatible API.
/// R2 requires DisablePayloadSigning and DisableDefaultChecksumValidation for uploads.
/// </summary>
public class R2StorageService : IR2StorageService
{
    private readonly R2Options _options;
    private IAmazonS3? _s3;

    public R2StorageService(IOptions<R2Options> options)
    {
        _options = options.Value;
    }

    private IAmazonS3 Client => _s3 ??= CreateClient();

    private IAmazonS3 CreateClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ServiceUrl))
            throw new InvalidOperationException("R2 is not configured. Set R2:AccountId, AccessKeyId, SecretAccessKey, and BucketName.");
        var credentials = new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey);
        return new AmazonS3Client(credentials, new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true
        });
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AccessKeyId) &&
        !string.IsNullOrWhiteSpace(_options.SecretAccessKey) &&
        !string.IsNullOrWhiteSpace(_options.BucketName);

    public async Task<string> UploadAsync(string key, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType ?? "application/octet-stream",
            // Required for R2: does not support Streaming SigV4
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        var response = await Client.PutObjectAsync(request, cancellationToken);
        return response.ETag ?? key;
    }

    public async Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await Client.GetObjectAsync(_options.BucketName, key, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<R2ObjectResult> GetWithMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await Client.GetObjectAsync(_options.BucketName, key, cancellationToken);
        var stream = new DisposeWithResponseStream(response.ResponseStream, response);
        return new R2ObjectResult
        {
            Stream = stream,
            ContentType = response.Headers.ContentType,
            ContentLength = response.ContentLength
        };
    }

    public async Task<IReadOnlyList<R2ObjectInfo>> ListAsync(string? prefix = null, CancellationToken cancellationToken = default)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = _options.BucketName,
            Prefix = prefix ?? ""
        };
        var list = new List<R2ObjectInfo>();
        ListObjectsV2Response response;
        do
        {
            response = await Client.ListObjectsV2Async(request, cancellationToken);
            foreach (var obj in response.S3Objects)
                list.Add(new R2ObjectInfo(obj.Key, obj.Size, obj.LastModified));
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated);

        return list;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        await Client.DeleteObjectAsync(_options.BucketName, key, cancellationToken);
    }

    public string GetPresignedDownloadUrl(string key, TimeSpan validity)
    {
        AWSConfigsS3.UseSignatureVersion4 = true;
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(validity)
        };
        return Client.GetPreSignedURL(request);
    }

    /// <summary>Stream wrapper that disposes an S3 response when the stream is disposed.</summary>
    private sealed class DisposeWithResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _response;

        public DisposeWithResponseStream(Stream inner, IDisposable response)
        {
            _inner = inner;
            _response = response;
        }

        public override void Close()
        {
            _inner.Close();
            _response.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }
            base.Dispose(disposing);
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) => _inner.ReadAsync(buffer, ct);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) => _inner.WriteAsync(buffer, ct);
    }
}
