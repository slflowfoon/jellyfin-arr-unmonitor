namespace ArrUnmonitor.Services;

public sealed class ConnectionTestResult
{
    public ConnectionTestResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }

    public string Message { get; }
}
