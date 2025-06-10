namespace DirectorySyncer;

public class ErrorOccurredEventArgs : EventArgs
{
    public string ErrorMessage { get; }

    public ErrorOccurredEventArgs(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }
}
