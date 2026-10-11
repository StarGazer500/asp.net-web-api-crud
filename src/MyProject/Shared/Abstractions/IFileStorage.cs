namespace MyProject.Shared.Abstractions;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream file, string name, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}
