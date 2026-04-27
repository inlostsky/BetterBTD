namespace BetterBTD.Models;

public sealed class LogEntry
{
    public required string Time { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
}
