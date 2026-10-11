namespace MyProject.Adapters.S3;

public class StorageOptions
{
    public string Bucket { get; set; } = "";
    public string LocalPath { get; set; } = "uploads";
}
