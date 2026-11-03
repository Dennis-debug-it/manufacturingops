namespace Ecosys.Infrastructure.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";
    public string LocalBasePath { get; set; } = "./storage/uploads";
    public string BaseUrl { get; set; } = "http://localhost:5000/files";
}
