namespace DirectorySyncer;

public class FileSyncedEventArgs : EventArgs
{
    public SyncFile SyncFile { get; }
    public int Index { get; init; }

    public FileSyncedEventArgs(SyncFile syncFile)
    {
        SyncFile = syncFile;
    }
}
