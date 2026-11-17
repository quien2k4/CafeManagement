using System.Windows;
using System.Windows.Controls;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class AdjustCustomerPointsWindow : Window
{
    private readonly Customer _customer;
    public int PointDelta { get; private set; }
    public string SelectedTier { get; private set; } = "BRONZE";

    public AdjustCustomerPointsWindow(Customer customer)
    {
        InitializeComponent();
        _customer = customer;

        TxtCustomerName.Text = customer.FullName;
        TxtCustomerPhone.Text = $"SĐT: {customer.PhoneNumber}";
        TxtCurrentPoints.Text = $"{customer.TotalPoints} điểm";
        SelectedTier = customer.MembershipTier;

        foreach (ComboBoxItem item in CboTier.Items)
        {
            if (item.Tag?.ToString() == customer.MembershipTier)
            {
                CboTier.SelectedItem = item;
                break;
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string raw = TxtPointDelta.Text.Trim().Replace("+", "");
        if (!int.TryParse(raw, out int delta))
        {
            MessageBox.Show("Vui lòng nhập số điểm thay đổi hợp lệ (số nguyên)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        PointDelta = delta;
        SelectedTier = (CboTier.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _customer.MembershipTier;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}