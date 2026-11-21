using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Wpf.Views.Inventory;

public partial class TransferSlipPreviewWindow : Window
{
    public StockTransferDto Transfer { get; }

    public TransferSlipPreviewWindow(StockTransferDto transfer)
    {
        InitializeComponent();
        Transfer = transfer;

        TxtTransferCode.Text = $"MÃ PHIẾU: {transfer.TransferCode}";
        TxtTransferDate.Text = $"Ngày lập: {transfer.RequestedDate.ToLocalTime():dd/MM/yyyy HH:mm}";

        TxtFromBranch.Text = $"🏢 Chi nhánh xuất: {transfer.FromBranchName}";
        TxtDispatchedBy.Text = $"Người xuất kho: {transfer.DispatchedByName ?? "Chưa xuất"}";
        TxtDispatchedDate.Text = transfer.DispatchedDate.HasValue ? $"Thời gian xuất: {transfer.DispatchedDate.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Thời gian xuất: --";

        TxtToBranch.Text = $"🏬 Chi nhánh nhận: {transfer.ToBranchName}";
        TxtReceivedBy.Text = $"Người kiểm đếm: {transfer.ReceivedByName ?? "Chưa nhận"}";
        TxtReceivedDate.Text = transfer.ReceivedDate.HasValue ? $"Thời gian nhận: {transfer.ReceivedDate.Value.ToLocalTime():dd/MM/yyyy HH:mm}" : "Thời gian nhận: --";

        TxtStatus.Text = transfer.StatusBadgeText;
        TxtRequestNote.Text = string.IsNullOrEmpty(transfer.RequestNote) ? "Ghi chú: Không có" : $"Ghi chú: {transfer.RequestNote}";

        GridDetails.ItemsSource = transfer.Details;

        TxtReconNote.Text = string.IsNullOrEmpty(transfer.ReconciliationNote)
            ? (transfer.HasDiscrepancy ? "⚠️ Biên bản ghi nhận có sai lệch số lượng giữa chi nhánh xuất và nhận." : "Biên bản đối soát: Khớp hoàn toàn số lượng xuất nhận.")
            : $"Biên bản đối soát: {transfer.ReconciliationNote}";

        TxtReqName.Text = transfer.RequestedByName;
        TxtDispName.Text = transfer.DispatchedByName ?? "(Chưa xuất)";
        TxtRecName.Text = transfer.ReceivedByName ?? "(Chưa nhận)";
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDlg = new PrintDialog();
            if (printDlg.ShowDialog() == true)
            {
                printDlg.PrintVisual(PrintArea, $"Phiếu điều chuyển {Transfer.TransferCode}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi in: {ex.Message}", "Lỗi In", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
