using System.Windows.Forms;

namespace SurfaceModeReceiver;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var application = new TrayApplicationContext(args);
        Application.Run(application);
    }
}
