using System.Windows;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views;

public partial class CheckoutWindow : Window
{
    public CheckoutViewModel ViewModel { get; }

    public CheckoutWindow(CheckoutViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.PaymentMethod == "CASH" && ViewModel.CustomerGivenAmount < ViewModel.FinalAmount)
        {
            MessageBox.Show($"Tiền khách đưa ({ViewModel.CustomerGivenAmount:N0} đ) chưa đủ để thanh toán ({ViewModel.FinalAmount:N0} đ)!", 
                "Chưa đủ tiền", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
