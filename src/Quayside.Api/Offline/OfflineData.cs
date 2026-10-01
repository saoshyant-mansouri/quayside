namespace Quayside.Api.Offline;

public sealed class OfflineData(IConfiguration configuration)
{
    public string Root { get; } = Locate(configuration);

    public string CorpusDirectory => Path.Combine(Root, "corpus");

    public string SchemaFile => Path.Combine(Root, "schema", "schema.json");

    private static string Locate(IConfiguration configuration)
    {
        var configured = configuration[OfflineServices.DataDirectoryKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "data", "schema", "schema.json")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new DirectoryNotFoundException($"data/schema/schema.json not found above the binaries; set {OfflineServices.DataDirectoryKey}.")
            : Path.Combine(directory.FullName, "data");
    }
}
