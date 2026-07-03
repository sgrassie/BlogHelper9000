namespace BlogHelper9000.Core.Models;

public sealed record FixMetadataSkip(string FilePath, string Reason);

public sealed class FixMetadataResult
{
    public List<string> Updated { get; } = [];
    public List<FixMetadataSkip> Skipped { get; } = [];
}
