using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SurfaceModeReceiver;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SurfaceModeServer _server;
    private readonly ReceiverConfig _config;
    private readonly ReceiverLog _log;

    public TrayApplicationContext(string[] args)
    {
        _log = new ReceiverLog();
        _config = ReceiverConfigStore.LoadOrCreate(_log);
        _server = new SurfaceModeServer(_config, _log);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "SurfaceModeReceiver"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Mode: starting", null, (_, _) => { });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copy pairing token", null, (_, _) => CopyTokenToClipboard());
        menu.Items.Add("Open config folder", null, (_, _) => OpenConfigFolder());
        menu.Items.Add("Restart server", null, async (_, _) => await RestartServerAsync());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += async (_, _) => await RestartServerAsync();

        _server.StateChanged += (_, state) => UpdateTrayText(state);

        Task.Run(async () =>
        {
            try
            {
                await _server.StartAsync();
                UpdateTrayText(_server.CurrentState);
                ShowStartupBalloon();
            }
            catch (Exception ex)
            {
                _log.Error("server", "startup", "unknown", "unknown", false, ex.Message);
                ShowErrorBalloon(ex.Message);
            }
        });
    }

    private void ShowStartupBalloon()
    {
        _notifyIcon.BalloonTipTitle = "SurfaceModeReceiver";
        _notifyIcon.BalloonTipText = $"Listening on {_server.ListenDescription}.";
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void ShowErrorBalloon(string message)
    {
        _notifyIcon.BalloonTipTitle = "SurfaceModeReceiver error";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void UpdateTrayText(ReceiverStateSnapshot state)
    {
        var mode = state.CurrentMode.ToString();
        var text = $"SurfaceModeReceiver - {mode}";
        if (text.Length > 63)
        {
            text = text[..63];
        }

        _notifyIcon.Text = text;

        if (_notifyIcon.ContextMenuStrip is { Items.Count: > 0 })
        {
            _notifyIcon.ContextMenuStrip.Items[0].Text = $"Mode: {mode}";
        }
    }

    private async Task RestartServerAsync()
    {
        await _server.RestartAsync();
        UpdateTrayText(_server.CurrentState);
    }

    private void CopyTokenToClipboard()
    {
        try
        {
            Clipboard.SetText(_config.GetToken());
            _notifyIcon.BalloonTipTitle = "SurfaceModeReceiver";
            _notifyIcon.BalloonTipText = "Pairing token copied to clipboard.";
            _notifyIcon.ShowBalloonTip(2000);
        }
        catch (Exception ex)
        {
            ShowErrorBalloon(ex.Message);
        }
    }

    private void OpenConfigFolder()
    {
        var path = ReceiverConfigStore.ConfigDirectory;
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _server.Dispose();
        base.ExitThreadCore();
    }
}
