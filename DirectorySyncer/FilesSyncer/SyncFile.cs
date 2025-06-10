namespace DirectorySyncer.FilesSyncer;

public record class SyncFile
{
    public string SourceDirectory { get; } = string.Empty;
    public string TargetDirectory { get; } = string.Empty;
    public string RelativePath { get; } = string.Empty;
    public string SourcePath => Path.Combine(SourceDirectory, RelativePath);
    public string TargetPath => Path.Combine(TargetDirectory, RelativePath);
    public SyncType SyncType { get; }

    public async Task SyncAsync(bool verify)
    {
        if (!File.Exists(SourcePath))
        {
            throw new FileNotFoundException($"Unable to start the syncing process because the source `{SourcePath}` is missing");
        }

        var targetDirectory = Path.GetDirectoryName(TargetPath);
        if (targetDirectory is not null && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        using (var sourceFS = File.OpenRead(SourcePath))
        {
            using (var targetFS = File.OpenWrite(TargetPath))
            {
                await sourceFS.CopyToAsync(targetFS);
            }
        }
        File.SetLastWriteTimeUtc(TargetPath, File.GetLastWriteTimeUtc(SourcePath));

        if (verify)
        {
            var isEqual = await CompareFileEquality(SourcePath, TargetPath);
            if (!isEqual)
            {
                throw new IOException($"Synced file `{TargetPath}` not equal to `{SourcePath}`");
            }
        }
    }

    public SyncFile(string sourceDirectory, string targetDirectory, string relativePath, SyncType syncType)
    {
        SourceDirectory = sourceDirectory;
        TargetDirectory = targetDirectory;
        RelativePath = relativePath;
        SyncType = syncType;
    }

    #region NonPublic
    private static async Task<bool> CompareFileEquality(string fileA, string fileB)
    {
        if (fileA == fileB)
        {
            return true;
        }

        using var fsA = File.OpenRead(fileA);
        using var fsB = File.OpenRead(fileB);

        if (fsA.Length != fsB.Length)
        {
            return false;
        }

        var length = fsA.Length;

        var bufferSize = 4096 * 1024;
        var bufferA = new byte[bufferSize]; // 4MB buffer
        var bufferB = new byte[bufferSize]; // 4MB buffer
        while (true)
        {
            var readCountA = await fsA.ReadAsync(bufferA.AsMemory(0, bufferSize));
            var readCountB = await fsB.ReadAsync(bufferB.AsMemory(0, bufferSize));
            if (readCountA != readCountB)
            {
                return false;
            }
            if (readCountA == 0)
            {
                break;
            }

            for (var i = 0; i < readCountA; i++)
            {
                if (bufferA[i] != bufferB[i])
                {
                    return false;
                }
            }
        }

        return true;
    }
    #endregion
}
