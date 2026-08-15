using System.Collections.Concurrent;

namespace SurfaceModeReceiver;

internal sealed class ModeStateMachine
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _nonces = new();
    private readonly TimeSpan _nonceRetention;

    public ModeStateMachine(TimeSpan nonceRetention)
    {
        _nonceRetention = nonceRetention;
    }

    public SurfaceMode CurrentMode { get; private set; } = SurfaceMode.Unknown;

    public ModeTransitionResult Apply(ModeCommand command, string? nonce, string source)
    {
        lock (_gate)
        {
            CleanupExpiredNonces();

            if (!string.IsNullOrWhiteSpace(nonce) && !_nonces.TryAdd(nonce, DateTimeOffset.UtcNow))
            {
                return ModeTransitionResult.Rejected("duplicate nonce", CurrentMode, CurrentMode, false);
            }

            if (command == ModeCommand.Ping)
            {
                return ModeTransitionResult.Accepted("pong", CurrentMode, CurrentMode, false);
            }

            var target = command == ModeCommand.Tablet ? SurfaceMode.Tablet : SurfaceMode.Laptop;
            var previous = CurrentMode;
            if (previous == target)
            {
                return ModeTransitionResult.Accepted("no-op", previous, target, false);
            }

            CurrentMode = target;
            return ModeTransitionResult.Accepted($"changed via {source}", previous, target, true);
        }
    }

    private void CleanupExpiredNonces()
    {
        if (_nonces.IsEmpty)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - _nonceRetention;
        foreach (var entry in _nonces)
        {
            if (entry.Value < cutoff)
            {
                _nonces.TryRemove(entry.Key, out _);
            }
        }
    }
}

internal sealed record ModeTransitionResult(
    bool Ok,
    string Message,
    SurfaceMode PreviousMode,
    SurfaceMode CurrentMode,
    bool Changed)
{
    public static ModeTransitionResult Accepted(string message, SurfaceMode previous, SurfaceMode current, bool changed) =>
        new(true, message, previous, current, changed);

    public static ModeTransitionResult Rejected(string message, SurfaceMode previous, SurfaceMode current, bool changed) =>
        new(false, message, previous, current, changed);
}
