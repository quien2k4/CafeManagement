using System.Windows;
using System.Windows.Threading;
using CafeManagement.Wpf.Views;
using CafeManagement.Wpf.Views.Authentication;

namespace CafeManagement.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RunLoginLoop();
    }

    private void RunLoginLoop()
    {
        while (true)
        {
            var login = new LoginWindow();
            if (login.ShowDialog() != true || login.AuthenticatedSession == null)
            {
                Shutdown();
                return;
            }

            var mainWin = new MainWindow(login.AuthenticatedSession);
            MainWindow = mainWin;
            mainWin.ShowDialog();

            if (mainWin.IsLogoutRequested)
            {
                continue;
            }

            Shutdown();
            break;
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Đã xảy ra lỗi: {e.Exception.Message}\n\nChi tiết:\n{e.Exception}", 
            "Thông Báo Lỗi", 
            MessageBoxButton.OK, 
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"Lỗi hệ thống: {ex.Message}\n\nChi tiết:\n{ex}", 
                "Lỗi Hệ Thống", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Lỗi tác vụ nền: {e.Exception.Message}", 
            "Lỗi Bất Đồng Bộ", 
            MessageBoxButton.OK, 
            MessageBoxImage.Error);

        e.SetObserved();
    }
}