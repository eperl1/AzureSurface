using System.Globalization;
using System.Text;

namespace SurfaceModeReceiver;

internal sealed class ReceiverLog
{
    private readonly object _gate = new();
    private readonly string _logPath;

    public ReceiverLog()
    {
        var dir = Path.Combine(ReceiverConfigStore.ConfigDirectory, "logs");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "receiver.log");
    }

    public void Info(string source, string eventName, string oldMode, string newMode, bool success, string message)
        => Write(source, eventName, oldMode, newMode, success, message);

    public void Error(string source, string eventName, string oldMode, string newMode, bool success, string message)
        => Write(source, eventName, oldMode, newMode, success, message);

    private void Write(string source, string eventName, string oldMode, string newMode, bool success, string message)
    {
        var line = string.Join(" | ", new[]
        {
            DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            $"source={source}",
            $"event={eventName}",
            $"old={oldMode}",
            $"new={newMode}",
            $"success={success}",
            $"message={message}"
        });

        lock (_gate)
        {
            File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
