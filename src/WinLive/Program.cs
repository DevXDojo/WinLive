using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;

namespace WinLive;

public static class Program
{
    private static App? _app;

    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        XamlCheckProcessRequirements();

        Microsoft.UI.Xaml.Application.Start(initializationParams =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _app = new App();
            _app.LaunchMainWindow();
        });
    }
}
