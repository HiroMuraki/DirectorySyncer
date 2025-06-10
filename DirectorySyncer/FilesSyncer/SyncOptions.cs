namespace DirectorySyncer.FilesSyncer;

[Flags]
public enum SyncOptions
{
#pragma warning disable format
    None   = 0b0000_0000,
    Verify = 0b0000_0001,
#pragma warning restore format
}
