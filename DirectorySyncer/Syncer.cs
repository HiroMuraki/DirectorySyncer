using System;
using System.Collections.Immutable;
using System.Data;
using System.Security.Principal;
using System.Text;

namespace DirectorySyncer;

public class Syncer
{
    public static Syncer Default { get; } = new();

    public event EventHandler<FileSyncedEventArgs>? FileSynced;
    public event EventHandler<ErrorOccuredEventArgs>? ErrorOccured;


    public async Task SyncFileAsync(SyncFile syncFile)
    {
        await SyncFileCoreAsync(syncFile, 0);
    }

    public async Task SyncFilesAsync(SyncFile[] syncFiles)
    {
        string[] missingFiles = syncFiles.Where(s => !File.Exists(s.SourcePath)).Select(s => s.SourcePath).ToArray();
        if (missingFiles.Any())
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Unable to start the syncing process because the following files are missing:");
            foreach (string? file in missingFiles)
            {
                sb.AppendLine(file);
            }
            throw new InvalidOperationException(sb.ToString());
        }

        for (int i = 0; i < syncFiles.Length; i++)
        {
            await SyncFileCoreAsync(syncFiles[i], i);
        }
    }

    public SyncFile[] GetDualSyncFiles(string directoryA, string directoryB)
    {
        var fromAtoB = GetSyncFilesCore(directoryA, directoryB);
        var fromBtoA = GetSyncFilesCore(directoryB, directoryA);

        return fromAtoB.Concat(fromBtoA).ToArray();
    }

    public SyncFile[] GetSyncFiles(string sourceDirectory, string targetDirectory)
    {
        return GetSyncFilesCore(sourceDirectory, targetDirectory);
    }

    #region NonPublic
    private static SyncFile[] GetSyncFilesCore(string sourceDirectory, string targetDirectory)
    {
        var sourceFiles = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(sourceDirectory, p)).ToImmutableArray();
        var targetFiles = Directory.GetFiles(targetDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(targetDirectory, p)).ToImmutableHashSet();

        var syncFiles = new List<SyncFile>();

        foreach (string? file in sourceFiles)
        {
            SyncMode? syncMode = null;

            if (!targetFiles.Contains(file))
            {
                syncMode = SyncMode.Copy;
            }
            else
            {
                var sourceLastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(sourceDirectory, file));
                var targetLastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(targetDirectory, file));
                if (sourceLastWriteTime > targetLastWriteTime)
                {
                    syncMode = SyncMode.Update;
                }
            }

            if (!syncMode.HasValue)
            {
                continue;
            }

            syncFiles.Add(new SyncFile()
            {
                SourceDirectory = sourceDirectory,
                TargetDirectory = targetDirectory,
                RelativePath = file,
                SyncMode = syncMode.Value,
            });
        }

        return syncFiles.ToArray();
    }
    private void OnFileSynced(SyncFile syncFile, int index)
    {
        FileSynced?.Invoke(this, new FileSyncedEventArgs(syncFile)
        {
            Index = index,
        });
    }
    private void OnErrorOccured(string errorMessage)
    {
        ErrorOccured?.Invoke(this, new ErrorOccuredEventArgs(errorMessage));
    }
    private async Task SyncFileCoreAsync(SyncFile syncFile, int index)
    {
        try
        {
            if (!File.Exists(syncFile.SourcePath))
            {
                OnErrorOccured($"Unable to start the syncing process because the source `{syncFile.SourcePath}` is missing");
                return;
            }

            string? targetDirectory = Path.GetDirectoryName(syncFile.TargetPath);
            if (targetDirectory is not null && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            using (var sourceFS = File.OpenRead(syncFile.SourcePath))
            {
                using (var targetFS = File.OpenWrite(syncFile.TargetPath))
                {
                    await sourceFS.CopyToAsync(targetFS);
                }
            }
            File.SetLastWriteTimeUtc(syncFile.TargetPath, File.GetLastWriteTimeUtc(syncFile.SourcePath));

            OnFileSynced(syncFile, index);
        }
        catch (Exception e)
        {
            OnErrorOccured(e.Message);
        }
    }
    #endregion
}
