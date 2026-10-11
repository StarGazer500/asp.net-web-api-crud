using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using MyProject.Shared.Abstractions;
using MyProject.Shared.Exceptions;

namespace MyProject.Adapters.S3;

internal class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.Bucket;

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket, Key = name, InputStream = file
            }, ct);
            return name;
        }
        catch (AmazonS3Exception ex)
        {
            throw new StorageException($"Upload failed: {ex.Message}");   // translate vendor errors
        }
    }

    public async Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        try
        {
            var res = await s3.GetObjectAsync(_bucket, name, ct);
            return res.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default) =>
        await s3.DeleteObjectAsync(_bucket, name, ct);
}
