using System.Windows;
using CafeManagement.Core.DTOs.Sync;

namespace CafeManagement.Wpf.Views.Orders;

public partial class SyncConflictDialog : Window
{
    public SyncConflictDetailDto ConflictDetail { get; }
    public string ResultChoice { get; private set; } = "REVIEW";

    public SyncConflictDialog(SyncConflictDetailDto conflict)
    {
        InitializeComponent();
        ConflictDetail = conflict;

        TxtOrderTitle.Text = $"Xung Đột Đơn Hàng: #{conflict.OrderNumber}";
        TxtConflictReason.Text = conflict.ConflictReason;

        // Local
        TxtLocalFinalAmount.Text = $"{conflict.LocalFinalAmount:N0} đ";
        TxtLocalSubTotal.Text = $"{conflict.LocalSubTotal:N0} đ";
        TxtLocalDiscount.Text = $"{conflict.LocalDiscount:N0} đ";
        TxtLocalPayment.Text = conflict.LocalPaymentMethod;
        TxtLocalItems.Text = conflict.LocalItemsSummary;

        // Server
        TxtServerFinalAmount.Text = $"{conflict.ServerFinalAmount:N0} đ";
        TxtServerSubTotal.Text = $"{conflict.ServerSubTotal:N0} đ";
        TxtServerDiscount.Text = $"{conflict.ServerDiscount:N0} đ";
        TxtServerPayment.Text = conflict.ServerPaymentMethod;
        TxtServerItems.Text = conflict.ServerItemsSummary;
    }

    private void UseLocalButton_Click(object sender, RoutedEventArgs e)
    {
        ResultChoice = "USE_LOCAL";
        DialogResult = true;
        Close();
    }

    private void UseServerButton_Click(object sender, RoutedEventArgs e)
    {
        ResultChoice = "USE_SERVER";
        DialogResult = true;
        Close();
    }

    private void ReviewButton_Click(object sender, RoutedEventArgs e)
    {
        ResultChoice = "REVIEW";
        DialogResult = false;
        Close();
    }
}
