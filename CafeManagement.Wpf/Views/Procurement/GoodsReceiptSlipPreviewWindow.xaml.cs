using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Wpf.Views.Procurement;

public class ReceiptPrintRow
{
    public int Stt { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal DeliveredQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? RejectionReason { get; set; }
    public string Uom { get; set; } = "g";
    public decimal UnitPrice { get; set; }
    public decimal TotalAcceptedPrice => AcceptedQuantity * UnitPrice;
}

public partial class GoodsReceiptSlipPreviewWindow : Window
{
    public GoodsReceiptSlipPreviewWindow(GoodsReceiptDto receipt)
    {
        InitializeComponent();

        TxtReceiptCode.Text = $"MÃ BIÊN BẢN: {receipt.ReceiptCode}";
        TxtReceiptDate.Text = $"Ngày nhận: {receipt.ReceivedDate:dd/MM/yyyy HH:mm}";
        TxtPoCode.Text = $"Đơn PO: {receipt.PoCode}";
        TxtSupplierName.Text = $"🏢 Nhà cung cấp: {receipt.SupplierName}";
        TxtDeliveryPerson.Text = $"Tài xế / Người giao: {receipt.DeliveryPerson ?? "Không rõ"}";
        TxtInvoiceNumber.Text = $"Hóa đơn / Phiếu xuất NCC: {receipt.InvoiceNumber ?? "Không có"}";
        TxtBranchName.Text = $"🏬 Chi nhánh nhập kho: {receipt.BranchName}";
        TxtReceiverName.Text = $"Thủ kho tiếp nhận & QC: {receipt.ReceivedByName}";
        TxtNote.Text = $"Ghi chú: {receipt.Note ?? "Không có"}";

        TxtTotalAmountDisplay.Text = $"{receipt.TotalAcceptedAmount:N0} đ";

        TxtSignDriver.Text = receipt.DeliveryPerson ?? "Đại diện NCC";
        TxtSignReceiver.Text = receipt.ReceivedByName;

        int stt = 1;
        var printRows = receipt.Details.Select(d => new ReceiptPrintRow
        {
            Stt = stt++,
            IngredientName = d.IngredientName,
            DeliveredQuantity = d.DeliveredQuantity,
            AcceptedQuantity = d.AcceptedQuantity,
            RejectedQuantity = d.RejectedQuantity,
            RejectionReason = d.RejectionReason,
            Uom = d.Uom,
            UnitPrice = d.UnitPrice
        }).ToList();

        DgReceiptItems.ItemsSource = printRows;
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(PrintArea, "In Bien Ban Nghiem Thu Hang Hoa");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi in biên bản: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
