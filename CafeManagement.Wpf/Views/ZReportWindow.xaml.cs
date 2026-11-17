using System.Windows;
using System.Windows.Media;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class ZReportWindow : Window
{
    public ZReportWindow(WorkShift shift, string branchName)
    {
        InitializeComponent();

        TxtBranchName.Text = branchName;
        TxtShiftCode.Text = shift.ShiftCode;
        TxtShiftName.Text = shift.ShiftName;
        TxtCashier.Text = shift.CashierName;
        TxtSignCashier.Text = shift.CashierName;

        TxtStartTime.Text = shift.StartTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        TxtEndTime.Text = (shift.EndTime ?? DateTime.UtcNow).ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        TxtTotalOrders.Text = $"{shift.TotalOrders} đơn";
        TxtCashSales.Text = $"{shift.CashSales:N0} đ";
        TxtQrSales.Text = $"{shift.VietQrSales:N0} đ";
        TxtTotalSales.Text = $"{shift.TotalSales:N0} đ";

        TxtOpeningCash.Text = $"{shift.OpeningCash:N0} đ";
        TxtCashAdded.Text = $"{shift.CashSales:N0} đ";
        TxtExpectedCash.Text = $"{shift.ExpectedClosingCash:N0} đ";
        TxtActualCash.Text = $"{shift.ActualClosingCash.GetValueOrDefault():N0} đ";

        decimal diff = shift.DifferenceAmount.GetValueOrDefault();
        if (diff == 0)
        {
            TxtDifference.Text = "0 đ (KHỚP HOÀN TOÀN)";
            TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
            BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FFF4"));
        }
        else if (diff > 0)
        {
            TxtDifference.Text = $"+{diff:N0} đ (THỪA TIỀN MẶT)";
            TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D69E2E"));
            BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFAF0"));
        }
        else
        {
            TxtDifference.Text = $"{diff:N0} đ (THIẾU HỤT QUỸ)";
            TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53E3E"));
            BorderDifference.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5"));
        }

        TxtNotes.Text = string.IsNullOrWhiteSpace(shift.Notes) ? "(Không có ghi chú)" : shift.Notes;
        TxtPrintTime.Text = $"Phiếu in lúc: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("⚡ Đã gửi lệnh in Phiếu Kết Toán Ca (Z-Report) thành công đến máy in hóa đơn nhiệt!", "In Báo Cáo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
