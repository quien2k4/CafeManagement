using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Wpf.Services.Printing;

namespace CafeManagement.Wpf.Views.Orders;

public partial class KitchenTicketWindow : Window
{
    private readonly OrderDto _order;
    private readonly List<KitchenTicketCupModel> _cups;

    public KitchenTicketWindow(OrderDto order)
    {
        InitializeComponent();

        _order = order;
        _cups = KitchenTicketService.BuildCups(order);

        TxtBranchName.Text = !string.IsNullOrWhiteSpace(order.BranchName) ? order.BranchName : "Chi nhánh";
        TxtOrderNumber.Text = $"#{order.OrderNumber}";
        TxtOrderTime.Text = order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
        TxtCashierName.Text = !string.IsNullOrWhiteSpace(order.CashierName) ? order.CashierName : "Thu ngân";

        string orderTypeDisplay = KitchenTicketService.GetOrderTypeDisplay(order.OrderType);
        TxtOrderType.Text = orderTypeDisplay;

        if (order.OrderType?.ToUpper() == "DINE_IN")
        {
            BadgeOrderType.Background = new SolidColorBrush(Color.FromRgb(235, 248, 255));
            BadgeOrderType.BorderBrush = new SolidColorBrush(Color.FromRgb(190, 227, 248));
            TxtOrderType.Foreground = new SolidColorBrush(Color.FromRgb(43, 108, 176));
        }
        else
        {
            BadgeOrderType.Background = new SolidColorBrush(Color.FromRgb(255, 245, 245));
            BadgeOrderType.BorderBrush = new SolidColorBrush(Color.FromRgb(254, 178, 178));
            TxtOrderType.Foreground = new SolidColorBrush(Color.FromRgb(229, 62, 62));
        }

        CupsItemsControl.ItemsSource = _cups;
        TxtTotalCupsFooter.Text = $"TỔNG SỐ LY CẦN PHA CHẾ:  {_cups.Count} ly)";
    }

    private void BtnExportDocx_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string docxPath = KitchenTicketService.GenerateDocx(_order);

            var res = MessageBox.Show(
                $"🎉 Đã xuất thành công Phiếu Pha Chế file Word (.docx)!\n\n" +
                $"• Đường dẫn: {docxPath}\n\n" +
                $"Bạn có muốn mở file Word ngay bây giờ không?",
                "Xuất Phiếu Pha Chế Thành Công",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(docxPath)
                {
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi xuất file Word: {ex.Message}", "Lỗi Xuất File", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrintPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                // In trực tiếp vùng TicketPrintArea (hỗ trợ in ra máy in quầy hoặc lưu qua Microsoft Print to PDF)
                printDlg.PrintVisual(TicketPrintArea, $"PhieuPhaChe_{_order.OrderNumber}");
                MessageBox.Show("Đã gửi lệnh in phiếu pha chế thành công!", "In Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi in phiếu: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}