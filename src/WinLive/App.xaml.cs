using Microsoft.UI.Xaml;

namespace WinLive;

public partial class App : Application
{
    private Window? _window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Program.Main owns startup for both unpackaged development runs and MSIX activation.
    }

    internal void LaunchMainWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
