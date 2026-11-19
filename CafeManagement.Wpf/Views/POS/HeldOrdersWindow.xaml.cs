using System.Windows;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views.POS;

public partial class HeldOrdersWindow : Window
{
    public HeldOrder? SelectedOrderToRestore { get; private set; }
    private readonly MainViewModel _mainVm;

    public HeldOrdersWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        _mainVm = mainVm;
        DataContext = mainVm;
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HeldOrder order)
        {
            SelectedOrderToRestore = order;
            DialogResult = true;
            Close();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is HeldOrder order)
        {
            var res = MessageBox.Show($"Bạn có chắc chắn muốn xóa đơn tạm #{order.OrderNumber}?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _mainVm.HeldOrders.Remove(order);
                _mainVm.UpdateHeldOrdersCount();
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
