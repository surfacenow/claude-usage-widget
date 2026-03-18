using System.Threading;
using System.Windows;

namespace FloatingClockWidget.Native;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = "FloatingClockWidget.Native.Singleton";
        var createdNew = false;
        _singleInstanceMutex = new Mutex(initiallyOwned: true, mutexName, out createdNew);

        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
            // no-op
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
        }

        base.OnExit(e);
    }
}
