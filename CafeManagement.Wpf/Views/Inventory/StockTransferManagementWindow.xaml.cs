using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Wpf.Services.Http;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views.Inventory;

public partial class StockTransferManagementWindow : Window
{
    public StockTransferManagementViewModel ViewModel { get; }

    public StockTransferManagementWindow(UserSession session)
    {
        InitializeComponent();
        ViewModel = new StockTransferManagementViewModel(session);
        DataContext = ViewModel;

        Loaded += async (s, e) => await ViewModel.LoadTransfersAsync();
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string status && ViewModel != null)
        {
            ViewModel.FilterByStatus(status);
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadTransfersAsync();
    }

    private async void BtnCreateRequest_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new CreateTransferRequestWindow(ViewModel.CurrentSession)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.ResultRequest != null)
        {
            try
            {
                var created = await StockTransferApiClient.Instance.CreateTransferRequestAsync(dlg.ResultRequest);
                if (created != null)
                {
                    MessageBox.Show($"Đã tạo yêu cầu điều chuyển thành công: {created.TransferCode}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    await ViewModel.LoadTransfersAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo yêu cầu điều chuyển: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnDispatch_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTransfer == null) return;
        var transfer = ViewModel.SelectedTransfer;

        var ans = MessageBox.Show(
            $"Bạn có chắc chắn muốn XUẤT KHO và VẬN CHUYỂN cho phiếu {transfer.TransferCode}?\n(Tồn kho của chi nhánh xuất '{transfer.FromBranchName}' sẽ bị trừ ngay lập tức)",
            "Xác Nhận Xuất Kho Vận Chuyển",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (ans != MessageBoxResult.Yes) return;

        try
        {
            var req = new DispatchStockTransferRequest
            {
                TransferId = transfer.TransferId,
                DispatchedNote = "Xuất kho giao vận chuyển"
            };

            var dispatched = await StockTransferApiClient.Instance.DispatchTransferAsync(req);
            if (dispatched != null)
            {
                MessageBox.Show($"Đã xuất kho thành công! Phiếu {dispatched.TransferCode} hiện ở trạng thái ĐANG VẬN CHUYỂN (IN_TRANSIT).", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                await ViewModel.LoadTransfersAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi xuất kho: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnReject_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTransfer == null) return;
        var transfer = ViewModel.SelectedTransfer;

        var ans = MessageBox.Show(
            $"Bạn có chắc chắn muốn TỪ CHỐI yêu cầu điều chuyển {transfer.TransferCode}?",
            "Xác Nhận Từ Chối",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (ans != MessageBoxResult.Yes) return;

        try
        {
            var req = new ApproveStockTransferRequest
            {
                TransferId = transfer.TransferId,
                IsApproved = false,
                ApprovalNote = "Chi nhánh nguồn không thể cung ứng vào lúc này"
            };

            var res = await StockTransferApiClient.Instance.ApproveTransferAsync(req);
            if (res != null)
            {
                MessageBox.Show($"Đã từ chối yêu cầu điều chuyển {res.TransferCode}.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await ViewModel.LoadTransfersAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi từ chối phiếu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnReconcile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTransfer == null) return;
        var transfer = ViewModel.SelectedTransfer;

        var dlg = new ReconcileTransferWindow(transfer)
        {
            Owner = this
        };

        if (dlg.ShowDialog() == true && dlg.ResultRequest != null)
        {
            try
            {
                var completed = await StockTransferApiClient.Instance.ReceiveAndReconcileAsync(dlg.ResultRequest);
                if (completed != null)
                {
                    string statusMsg = completed.HasDiscrepancy ? "⚠️ Đã nhận hàng và ghi nhận đối soát chênh lệch!" : "✅ Đã nhận đủ 100% và nhập kho chi nhánh đích thành công!";
                    MessageBox.Show($"{statusMsg}\nPhiếu: {completed.TransferCode}", "Hoàn Tất Đối Soát", MessageBoxButton.OK, MessageBoxImage.Information);
                    await ViewModel.LoadTransfersAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nhận hàng & đối soát: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnPrintSlip_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTransfer == null) return;
        var dlg = new TransferSlipPreviewWindow(ViewModel.SelectedTransfer)
        {
            Owner = this
        };
        dlg.ShowDialog();
    }

    private async void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTransfer == null) return;
        var transfer = ViewModel.SelectedTransfer;

        var ans = MessageBox.Show(
            $"Bạn có chắc chắn muốn HỦY phiếu điều chuyển {transfer.TransferCode}?\n(Nếu hàng đã xuất kho thì tồn kho sẽ được hoàn trả lại cho chi nhánh xuất)",
            "Xác Nhận Hủy Phiếu",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (ans != MessageBoxResult.Yes) return;

        try
        {
            bool ok = await StockTransferApiClient.Instance.CancelTransferAsync(transfer.TransferId, "Người dùng hủy phiếu");
            if (ok)
            {
                MessageBox.Show("Đã hủy phiếu điều chuyển thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await ViewModel.LoadTransfersAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi hủy phiếu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
