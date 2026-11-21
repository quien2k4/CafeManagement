using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Procurement;
using CafeManagement.Wpf.Services.Http;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views.Procurement;

public partial class ProcurementManagementWindow : Window
{
    private readonly ProcurementManagementViewModel _vm;

    public ProcurementManagementWindow(UserSession session)
    {
        InitializeComponent();

        _vm = new ProcurementManagementViewModel(session);
        DataContext = _vm;

        Loaded += async (s, e) => await _vm.InitializeAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Tab 1: Nhà Cung Cấp & Bảng Giá
    private async void AddSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SupplierEditWindow();
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var created = await ProcurementApiClient.Instance.CreateSupplierAsync(dlg.RequestData);
            if (created != null)
            {
                MessageBox.Show($"Đã tạo Nhà Cung Cấp '{created.SupplierName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadSuppliersAsync();
            }
        }
    }

    private async void EditSupplierButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedSupplier == null) return;

        var dlg = new SupplierEditWindow(_vm.SelectedSupplier);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var updated = await ProcurementApiClient.Instance.UpdateSupplierAsync(_vm.SelectedSupplier.SupplierId, dlg.RequestData);
            if (updated != null)
            {
                MessageBox.Show($"Đã cập nhật Nhà Cung Cấp '{updated.SupplierName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadSuppliersAsync();
            }
        }
    }

    private async void AddSupplierIngredientButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedSupplier == null)
        {
            MessageBox.Show("Vui lòng chọn một Nhà Cung Cấp trước khi thiết lập bảng giá.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var allIngredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: true);
        var dlg = new SupplierIngredientEditWindow(_vm.SelectedSupplier.SupplierId, allIngredients);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var result = await ProcurementApiClient.Instance.SetSupplierIngredientAsync(dlg.RequestData);
            if (result != null)
            {
                await _vm.LoadSupplierIngredientsAsync();
                await _vm.LoadSuppliersAsync();
            }
        }
    }

    private async void EditSupplierIngredientButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedSupplier == null || _vm.SelectedSupplierIngredient == null) return;

        var allIngredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: true);
        var dlg = new SupplierIngredientEditWindow(_vm.SelectedSupplier.SupplierId, allIngredients, _vm.SelectedSupplierIngredient);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var result = await ProcurementApiClient.Instance.SetSupplierIngredientAsync(dlg.RequestData);
            if (result != null)
            {
                await _vm.LoadSupplierIngredientsAsync();
            }
        }
    }
    #endregion

    #region Tab 2: Đơn Đặt Hàng Mua (PO)
    private void FilterPoStatus_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string status)
        {
            _vm.FilterPoStatus = status;
        }
    }

    private async void CreatePoButton_Click(object sender, RoutedEventArgs e)
    {
        var suppliers = await ProcurementApiClient.Instance.GetSuppliersAsync(activeOnly: true);
        var branches = await AuthenticationApiClient.Instance.GetBranchesAsync();

        if (suppliers.Count == 0)
        {
            MessageBox.Show("Hệ thống chưa có Nhà Cung Cấp nào đang hoạt động. Vui lòng tạo Nhà Cung Cấp trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int defaultBranchId = _vm.CurrentSession.BranchId > 0 ? _vm.CurrentSession.BranchId : (branches.Count > 0 ? branches[0].BranchId : 1);

        var dlg = new CreatePurchaseOrderWindow(suppliers, branches, defaultBranchId, _vm.CurrentSession.UserId);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var po = await ProcurementApiClient.Instance.CreatePurchaseOrderAsync(dlg.RequestData);
            if (po != null)
            {
                MessageBox.Show($"Đã lập Đơn Đặt Hàng '{po.PoCode}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await _vm.LoadPurchaseOrdersAsync();

                // Gợi ý xem/in đơn ngay
                var res = MessageBox.Show("Bạn có muốn xem trước và in Đơn Đặt Hàng PO để gửi NCC ngay không?", "In Đơn PO", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    var printDlg = new PurchaseOrderPrintWindow(po);
                    printDlg.Owner = this;
                    printDlg.ShowDialog();
                }
            }
        }
    }

    private async void CancelPoButton_Click(object sender, RoutedEventArgs e)
    {
        await _vm.CancelPurchaseOrderAsync();
    }

    private void PrintPoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedPurchaseOrder == null)
        {
            MessageBox.Show("Vui lòng chọn một đơn đặt hàng để in.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var printDlg = new PurchaseOrderPrintWindow(_vm.SelectedPurchaseOrder);
        printDlg.Owner = this;
        printDlg.ShowDialog();
    }
    #endregion

    #region Tab 3: Tiếp Nhận Hàng Hóa & QC (2 Bước)
    private async void StartInspectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedPendingPo == null)
        {
            MessageBox.Show("Vui lòng chọn một đơn PO trong danh sách đang chờ tiếp nhận.", "Chưa chọn đơn", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new GoodsReceiptInspectionWindow(_vm.SelectedPendingPo, _vm.CurrentSession);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            var gr = await ProcurementApiClient.Instance.ProcessGoodsReceiptAsync(dlg.RequestData);
            if (gr != null)
            {
                MessageBox.Show(
                    $"✅ Tiếp nhận & Ghi sổ kho thành công!\n" +
                    $"• Mã biên bản: {gr.ReceiptCode}\n" +
                    $"• Tổng tiền nghiệm thu ghi sổ: {gr.TotalAcceptedAmount:N0} đ\n" +
                    $"Hệ thống đã tự động cộng số lượng hàng Đạt Chuẩn vào tồn kho chi nhánh.",
                    "Hoàn tất tiếp nhận", MessageBoxButton.OK, MessageBoxImage.Information);

                await _vm.LoadPurchaseOrdersAsync();
                await _vm.LoadGoodsReceiptsAsync();

                // Mở biên bản nghiệm thu để xem và in ấn
                var printDlg = new GoodsReceiptSlipPreviewWindow(gr);
                printDlg.Owner = this;
                printDlg.ShowDialog();
            }
        }
    }

    private void PrintReceiptSlipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedReceipt == null)
        {
            MessageBox.Show("Vui lòng chọn một biên bản tiếp nhận để in.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var printDlg = new GoodsReceiptSlipPreviewWindow(_vm.SelectedReceipt);
        printDlg.Owner = this;
        printDlg.ShowDialog();
    }
    #endregion
}
