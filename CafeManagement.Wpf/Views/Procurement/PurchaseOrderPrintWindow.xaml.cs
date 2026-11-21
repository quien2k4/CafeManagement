using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Wpf.Views.Procurement;

public class PoPrintRow
{
    public int Stt { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public string Uom { get; set; } = "g";
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

public partial class PurchaseOrderPrintWindow : Window
{
    public PurchaseOrderPrintWindow(PurchaseOrderDto po)
    {
        InitializeComponent();

        TxtPoCode.Text = $"MÃ ĐƠN PO: {po.PoCode}";
        TxtOrderDate.Text = $"Ngày đặt: {po.OrderDate:dd/MM/yyyy HH:mm}";
        TxtExpectedDelivery.Text = po.ExpectedDeliveryDate.HasValue
            ? $"Hẹn giao: {po.ExpectedDeliveryDate.Value:dd/MM/yyyy}"
            : "Hẹn giao: Theo thỏa thuận";

        TxtSupplierName.Text = $"🏢 Kính gửi NCC: {po.SupplierName}";
        TxtCreatedBy.Text = $"Người lập đơn: {po.CreatedByName}";
        TxtBranchName.Text = $"🏬 Địa điểm nhận hàng: {po.BranchName}";
        TxtNote.Text = $"Ghi chú: {po.Note ?? "Không có"}";

        TxtSubTotal.Text = $"{po.SubTotal:N0} đ";
        TxtDiscount.Text = $"{po.DiscountAmount:N0} đ";
        TxtTax.Text = $"{po.TaxAmount:N0} đ";
        TxtTotalAmount.Text = $"{po.TotalAmount:N0} đ";

        TxtSignBuyer.Text = po.CreatedByName;

        int stt = 1;
        var printRows = po.Details.Select(d => new PoPrintRow
        {
            Stt = stt++,
            IngredientCode = d.IngredientCode,
            IngredientName = d.IngredientName,
            OrderedQuantity = d.OrderedQuantity,
            Uom = d.Uom,
            UnitPrice = d.UnitPrice,
            SubTotal = d.SubTotal
        }).ToList();

        DgPoItems.ItemsSource = printRows;
    }

    private void PrintButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(PrintArea, "In Don Dat Hang Mua PO");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi in đơn đặt hàng: {ex.Message}", "Lỗi In Ấn", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
