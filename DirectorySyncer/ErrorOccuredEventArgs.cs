namespace DirectorySyncer;

public class ErrorOccuredEventArgs : EventArgs
{
    public string ErrorMessage { get; }

    public ErrorOccuredEventArgs(string errorMessage)
    {
        ErrorMessage = errorMessage;
    }
}
