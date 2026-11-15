using System.Windows;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views;

public partial class CustomizeProductWindow : Window
{
    public CustomizeProductViewModel ViewModel { get; }

    public CustomizeProductWindow(CustomizeProductViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
