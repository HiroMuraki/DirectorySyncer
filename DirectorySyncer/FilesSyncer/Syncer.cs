using System.Collections.Immutable;
using System.Data;
using System.Text;

namespace DirectorySyncer.FilesSyncer;

public class Syncer
{
    public const int TaskDelay = 1;

    public static Syncer Default { get; } = new();

    public event EventHandler<FileSyncedEventArgs>? FileSynced;
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccured;
    public event EventHandler<WorkingProgressChangedEventArgs>? WorkingProgressChanged;

    public IReadOnlyList<SyncFile> SyncFiles => _syncFiles;
    public string SourceDirectory { get; set; } = string.Empty;
    public string TargetDirectory { get; set; } = string.Empty;
    public Mode Mode { get; set; } = Mode.SourceToTarget;
    public SyncOptions SyncOptions { get; set; } = SyncOptions.None;

    public void UpdateSyncFiles()
    {
        SyncFile[] syncFiles;
        if (Mode == Mode.SourceToTarget)
        {
            syncFiles = GetSyncFilesCore(SourceDirectory, TargetDirectory);
        }
        else if (Mode == Mode.Dual)
        {
            var fromAtoB = GetSyncFilesCore(SourceDirectory, TargetDirectory);
            var fromBtoA = GetSyncFilesCore(TargetDirectory, SourceDirectory);

            syncFiles = fromAtoB.Concat(fromBtoA).ToArray();
        }
        else
        {
            throw new InvalidOperationException($"Invalid Mode `{Mode}`");
        }

        _syncFiles.Clear();
        foreach (var item in syncFiles)
        {
            _syncFiles.Add(item);
        }
    }

    public async Task SyncAllFilesAsync()
    {
        var missingFiles = SyncFiles.Where(s => !File.Exists(s.SourcePath)).Select(s => s.SourcePath).ToArray();
        if (missingFiles.Any())
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Unable to start the syncing process because the following files are missing:");
            foreach (var file in missingFiles)
            {
                sb.AppendLine(file);
            }
            throw new InvalidOperationException(sb.ToString());
        }

        OnWorkingProgressChanged(0);
        for (var i = 0; i < SyncFiles.Count; i++)
        {
            await SyncFileCoreAsync(SyncFiles[i], i);
            OnWorkingProgressChanged((i + 1.0) / SyncFiles.Count);
            await Task.Delay(TaskDelay);
        }
        OnWorkingProgressChanged(1);
    }

    #region NonPublic
    private readonly List<SyncFile> _syncFiles = new();
    private SyncFile[] GetSyncFilesCore(string sourceDirectory, string targetDirectory)
    {
        var sourceFiles = GetFiles(sourceDirectory);
        var targetFiles = GetFiles(targetDirectory);

        var syncFiles = new List<SyncFile>();

        foreach (var file in sourceFiles)
        {
            SyncType? syncType = null;

            if (!targetFiles.Contains(file))
            {
                syncType = SyncType.Copy;
            }
            else
            {
                var sourceLastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(sourceDirectory, file));
                var targetLastWriteTime = File.GetLastWriteTimeUtc(Path.Combine(targetDirectory, file));
                if (sourceLastWriteTime > targetLastWriteTime)
                {
                    syncType = SyncType.Update;
                }
            }

            if (!syncType.HasValue)
            {
                continue;
            }

            syncFiles.Add(new SyncFile(sourceDirectory, targetDirectory, file, syncType.Value));
        }

        return syncFiles.ToArray();

        static ImmutableArray<string> GetFiles(string directory)
        {
            var enumerationOptions = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
            };

            return Directory.GetFiles(directory, "*", enumerationOptions)
                .Select(p => Path.GetRelativePath(directory, p)).ToImmutableArray();
        }
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
        ErrorOccured?.Invoke(this, new ErrorOccurredEventArgs(errorMessage));
    }
    private void OnWorkingProgressChanged(double value)
    {
        WorkingProgressChanged?.Invoke(this, new WorkingProgressChangedEventArgs(value));
    }
    private async Task SyncFileCoreAsync(SyncFile syncFile, int index)
    {
        try
        {
            await syncFile.SyncAsync(HasOption(SyncOptions.Verify));
            OnFileSynced(syncFile, index);
        }
        catch (Exception e)
        {
            OnErrorOccured(e.Message);
        }
    }
    private bool HasOption(SyncOptions option)
    {
        return (SyncOptions & option) == option;
    }
    #endregion
}
