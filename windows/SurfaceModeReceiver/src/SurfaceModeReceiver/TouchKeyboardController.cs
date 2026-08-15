using System.Diagnostics;

namespace SurfaceModeReceiver;

internal sealed class TouchKeyboardController
{
    public void Show()
    {
        StartKeyboardProcess();
    }

    public void Hide()
    {
        foreach (var processName in new[] { "TabTip", "TextInputHost" })
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                        if (!process.WaitForExit(1000))
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                }
                catch
                {
                    // best-effort only
                }
            }
        }
    }

    private static void StartKeyboardProcess()
    {
        var keyboardPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            @"microsoft shared\ink\TabTip.exe");

        if (File.Exists(keyboardPath))
        {
            Process.Start(new ProcessStartInfo(keyboardPath) { UseShellExecute = true });
        }
    }
}
