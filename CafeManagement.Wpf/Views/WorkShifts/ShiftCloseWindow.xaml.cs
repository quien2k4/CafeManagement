using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.WorkShifts;

public partial class ShiftCloseWindow : Window
{
    public WorkShift Shift { get; }
    public decimal ActualCash { get; private set; }
    public decimal Difference { get; private set; }
    public string ClosingNotes { get; private set; } = string.Empty;

    public ShiftCloseWindow(WorkShift shift)
    {
        InitializeComponent();
        Shift = shift;

        TxtShiftCode.Text = shift.ShiftCode;
        TxtCashier.Text = shift.CashierName;
        TxtShiftName.Text = shift.ShiftName;
        TxtShiftDuration.Text = $"{shift.StartTime.ToLocalTime():HH:mm dd/MM} -> Hiện tại";

        TxtOpeningCash.Text = $"{shift.OpeningCash:N0} đ";
        TxtCashSales.Text = $"{shift.CashSales:N0} đ";
        TxtVietQrSales.Text = $"{shift.VietQrSales:N0} đ";
        TxtTotalOrders.Text = $"{shift.TotalOrders} đơn";

        decimal expected = shift.OpeningCash + shift.CashSales;
        TxtExpectedCash.Text = $"{expected:N0} đ";

        // Mặc định điền số tiền kỳ vọng để thu ngân kiểm tra
        TxtActualCash.Text = $"{expected:N0}";
        UpdateDifference();
    }

    private void TxtActualCash_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDifference();
    }

    private void UpdateDifference()
    {
        if (TxtActualCash == null || TxtDifference == null || BorderDifference == null) return;

        string raw = TxtActualCash.Text.Replace(",", "").Replace(".", "").Trim();
        if (decimal.TryParse(raw, out decimal actual))
        {
            decimal expected = Shift.OpeningCash + Shift.CashSales;
            decimal diff = actual - expected;
            ActualCash = actual;
            Difference = diff;

            if (diff == 0)
            {
                TxtDifference.Text = "✅ Khớp tiền (Chênh lệch: 0 đ)";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FFF4"));
                BorderDifference.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C6F6D5"));
            }
            else if (diff > 0)
            {
                TxtDifference.Text = $"⚠️ Thừa tiền mặt: +{diff:N0} đ";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D69E2E"));
                BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFAF0"));
                BorderDifference.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEEBC8"));
            }
            else
            {
                TxtDifference.Text = $"⛔ Thiếu hụt quỹ: {diff:N0} đ";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
                BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5"));
                BorderDifference.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FED7D7"));
            }
        }
        else
        {
            TxtDifference.Text = "Vui lòng nhập số tiền hợp lệ";
            TxtDifference.Foreground = Brushes.Gray;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseShift_Click(object sender, RoutedEventArgs e)
    {
        string raw = TxtActualCash.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(raw, out decimal actual) || actual < 0)
        {
            MessageBox.Show("Vui lòng nhập số tiền mặt thực tế kiểm đếm hợp lệ!", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtActualCash.Focus();
            return;
        }

        ActualCash = actual;
        ClosingNotes = TxtNotes.Text.Trim();

        var res = MessageBox.Show($"Bạn có chắc chắn muốn KẾT CA làm việc #{Shift.ShiftCode} ngay bây giờ?\n\n" +
                                  $"• Doanh thu ca: {Shift.TotalSales:N0} đ ({Shift.TotalOrders} đơn)\n" +
                                  $"• Tiền mặt thực tế bàn giao: {ActualCash:N0} đ\n" +
                                  $"• Chênh lệch quỹ: {(Difference >= 0 ? "+" : "")}{Difference:N0} đ\n\n" +
                                  "Sau khi đóng ca, hệ thống sẽ chốt sổ và xuất Phiếu Z-Report.",
                                  "Xác nhận đóng ca", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res == MessageBoxResult.Yes)
        {
            DialogResult = true;
            Close();
        }
    }
}
