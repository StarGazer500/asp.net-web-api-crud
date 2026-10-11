using Microsoft.Extensions.Options;
using MyProject.Adapters.S3;
using MyProject.Shared.Abstractions;

namespace MyProject.Adapters.Local;

// Disk-based storage for local development, so no AWS account is needed.
internal class LocalFileStorage(IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.LocalPath);

    public async Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default)
    {
        var path = ResolvePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = File.Create(path);
        await file.CopyToAsync(target, ct);
        return name;
    }

    public Task<Stream?> DownloadAsync(string name, CancellationToken ct = default)
    {
        var path = ResolvePath(name);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        File.Delete(ResolvePath(name));
        return Task.CompletedTask;
    }

    private string ResolvePath(string name)
    {
        var path = Path.GetFullPath(Path.Combine(_root, name));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar))
            throw new ArgumentException($"Invalid file name: {name}");
        return path;
    }
}
