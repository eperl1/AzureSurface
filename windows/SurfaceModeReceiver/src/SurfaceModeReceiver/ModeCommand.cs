namespace SurfaceModeReceiver;

internal enum ModeCommand
{
    Tablet,
    Laptop,
    Ping
}

internal enum SurfaceMode
{
    Unknown,
    Tablet,
    Laptop
}

internal sealed record ModeRequest(string Command, string TimestampUtc, string Nonce, string? Source);

internal sealed record ModeResponse(
    bool Ok,
    string Message,
    string CurrentMode,
    string? PreviousMode = null,
    bool Changed = false);
