using System.Windows;
using CafeManagement.Core.Models;
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
                "B?n có ch?c ch?n mu?n dang xu?t kh?i h? th?ng?", 
                "Xác Nh?n Ðang Xu?t", 
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
