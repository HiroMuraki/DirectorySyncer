namespace DirectorySyncer;

public record class SyncFile
{
    public required string SourceDirectory { get; init; } = string.Empty;
    public required string TargetDirectory { get; init; } = string.Empty;
    public required string RelativePath { get; init; } = string.Empty;
    public string SourcePath => Path.Combine(SourceDirectory, RelativePath);
    public string TargetPath => Path.Combine(TargetDirectory, RelativePath);
    public SyncMode SyncMode { get; init; }
}
