namespace DirectorySyncer.FilesSyncer;

public class WorkingProgressChangedEventArgs : EventArgs
{
    public double Progress { get; }

    public WorkingProgressChangedEventArgs(double progress)
    {
        Progress = progress;
    }
}
