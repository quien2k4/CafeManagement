using System.Windows;
using CafeManagement.Wpf.Models;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf;

public partial class MainWindow : Window
{
    public bool IsLogoutRequested { get; private set; }

    public MainWindow() : this(null)
    {
    }

    public MainWindow(UserSession? session)
    {
        InitializeComponent();

        var vm = new MainViewModel(session);
        vm.RequestLogout += () =>
        {
            var ask = MessageBox.Show(
                "Bạn có chắc chắn muốn đăng xuất khỏi hệ thống?", 
                "Xác Nhận Đăng Xuất", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (ask == MessageBoxResult.Yes)
            {
                IsLogoutRequested = true;
                Close();
            }
        };

        DataContext = vm;
    }
}
