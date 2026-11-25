using System.Windows;
using System.Windows.Controls;
using CafeManagement.Wpf.ViewModels.Employees;

namespace CafeManagement.Wpf.Views.Employees;

public partial class StaffingAnalyticsWindow : Window
{
    private readonly StaffingAnalyticsViewModel _viewModel;

    public StaffingAnalyticsWindow(int? defaultBranchId = null)
    {
        InitializeComponent();
        _viewModel = new StaffingAnalyticsViewModel();
        DataContext = _viewModel;

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync(defaultBranchId);
        };
    }

    private async void BranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && _viewModel != null)
        {
            await _viewModel.LoadAnalyticsAsync();
        }
    }

    private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CheckSelectedDateHoliday();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
