using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.WorkShifts;

public partial class ShiftOpenWindow : Window
{
    public WorkShift? CreatedShift { get; private set; }

    private readonly int _branchId;
    private readonly int _cashierUserId;
    private readonly string _cashierName;
    private readonly string _branchName;

    public ShiftOpenWindow(int branchId, string branchName, int cashierUserId, string cashierName)
    {
        InitializeComponent();
        _branchId = branchId;
        _branchName = branchName;
        _cashierUserId = cashierUserId;
        _cashierName = cashierName;

        TxtCashier.Text = _cashierName;
        TxtBranch.Text = _branchName;
    }

    private void QuickCash_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cashStr && decimal.TryParse(cashStr, out decimal cash))
        {
            TxtOpeningCash.Text = $"{cash:N0}";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OpenShift_Click(object sender, RoutedEventArgs e)
    {
        string rawCash = TxtOpeningCash.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawCash, out decimal openingCash) || openingCash < 0)
        {
            MessageBox.Show("Vui lòng nhập số tiền bàn giao đầu ca hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOpeningCash.Focus();
            return;
        }

        string shiftName = (CboShiftName.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Ca Sáng";
        string shiftCode = $"CA{DateTime.UtcNow:yyMMddHHmm}-{Random.Shared.Next(100, 999)}";

        CreatedShift = new WorkShift
        {
            ShiftCode = shiftCode,
            BranchId = _branchId,
            CashierUserId = _cashierUserId,
            CashierName = _cashierName,
            ShiftName = shiftName,
            StartTime = DateTime.UtcNow,
            OpeningCash = openingCash,
            CashSales = 0,
            VietQrSales = 0,
            TotalSales = 0,
            TotalOrders = 0,
            ExpectedClosingCash = openingCash,
            Status = "OPEN",
            Notes = TxtNotes.Text.Trim()
        };

        DialogResult = true;
        Close();
    }
}
