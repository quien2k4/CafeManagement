using System.Windows;
using System.Windows.Controls;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class AddCustomerWindow : Window
{
    public Customer? CreatedCustomer { get; private set; }

    public AddCustomerWindow(string initialPhone = "")
    {
        InitializeComponent();
        TxtPhoneNumber.Text = initialPhone.Trim();
        if (!string.IsNullOrEmpty(TxtPhoneNumber.Text))
        {
            TxtFullName.Focus();
        }
        else
        {
            TxtPhoneNumber.Focus();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string phone = TxtPhoneNumber.Text.Trim();
        string name = TxtFullName.Text.Trim();

        if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập đầy đủ Số điện thoại và Họ tên khách hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string tier = (CboTier.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "BRONZE";

        CreatedCustomer = new Customer
        {
            PhoneNumber = phone,
            FullName = name,
            MembershipTier = tier,
            TotalPoints = 0,
            LifetimePoints = 0,
            TotalSpent = 0,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}