namespace Quayside.Infrastructure.Configuration;

public sealed record DataPaths(string Root)
{
    public string CorpusDirectory => Path.Combine(Root, "corpus");

    public string SchemaFile => Path.Combine(Root, "schema", "schema.json");

    public static DataPaths Resolve(string configuredDirectory, string startDirectory)
    {
        if (Directory.Exists(configuredDirectory))
            return new DataPaths(configuredDirectory);

        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "schema", "schema.json")))
                return new DataPaths(candidate);
            directory = directory.Parent;
        }

        return new DataPaths(configuredDirectory);
    }
}
