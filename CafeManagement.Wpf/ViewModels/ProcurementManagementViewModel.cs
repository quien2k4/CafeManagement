using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Procurement;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.ViewModels;

public partial class ProcurementManagementViewModel : ObservableObject
{
    public UserSession CurrentSession { get; }

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private bool _isLoading;

    #region Tab 1: Nhà Cung Cấp & Bảng Giá
    [ObservableProperty]
    private ObservableCollection<SupplierDto> _suppliers = [];

    private List<SupplierDto> _allSuppliers = [];

    [ObservableProperty]
    private SupplierDto? _selectedSupplier;

    [ObservableProperty]
    private ObservableCollection<SupplierIngredientDto> _supplierIngredients = [];

    [ObservableProperty]
    private SupplierIngredientDto? _selectedSupplierIngredient;

    [ObservableProperty]
    private string _supplierSearchKeyword = string.Empty;
    #endregion

    #region Tab 2: Đơn Đặt Hàng Mua (PO)
    [ObservableProperty]
    private ObservableCollection<PurchaseOrderDto> _purchaseOrders = [];

    private List<PurchaseOrderDto> _allPurchaseOrders = [];

    [ObservableProperty]
    private PurchaseOrderDto? _selectedPurchaseOrder;

    [ObservableProperty]
    private string _filterPoStatus = "ALL";

    [ObservableProperty]
    private string _poSearchKeyword = string.Empty;

    [ObservableProperty]
    private int _poOrderedCount;

    [ObservableProperty]
    private int _poPartiallyReceivedCount;

    [ObservableProperty]
    private int _poCompletedCount;
    #endregion

    #region Tab 3: Tiếp Nhận Hàng Hóa & QC
    [ObservableProperty]
    private ObservableCollection<PurchaseOrderDto> _pendingOrdersForReceipt = [];

    [ObservableProperty]
    private PurchaseOrderDto? _selectedPendingPo;

    [ObservableProperty]
    private ObservableCollection<GoodsReceiptDto> _goodsReceipts = [];

    private List<GoodsReceiptDto> _allReceipts = [];

    [ObservableProperty]
    private GoodsReceiptDto? _selectedReceipt;

    [ObservableProperty]
    private string _receiptSearchKeyword = string.Empty;
    #endregion

    public ProcurementManagementViewModel(UserSession session)
    {
        CurrentSession = session;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await Task.WhenAll(
                LoadSuppliersAsync(),
                LoadPurchaseOrdersAsync(),
                LoadGoodsReceiptsAsync()
            );
        }
        finally
        {
            IsLoading = false;
        }
    }

    #region Tab 1 Commands & Handlers
    [RelayCommand]
    public async Task LoadSuppliersAsync()
    {
        var list = await ProcurementApiClient.Instance.GetSuppliersAsync();
        _allSuppliers = list;
        FilterSuppliers();

        if (SelectedSupplier == null && Suppliers.Count > 0)
        {
            SelectedSupplier = Suppliers[0];
        }
    }

    partial void OnSupplierSearchKeywordChanged(string value)
    {
        FilterSuppliers();
    }

    private void FilterSuppliers()
    {
        var filtered = _allSuppliers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SupplierSearchKeyword))
        {
            string kw = SupplierSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(s =>
                s.SupplierCode.ToLower().Contains(kw) ||
                s.SupplierName.ToLower().Contains(kw) ||
                (s.Phone != null && s.Phone.Contains(kw)) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(kw)));
        }

        Suppliers = new ObservableCollection<SupplierDto>(filtered);
    }

    partial void OnSelectedSupplierChanged(SupplierDto? value)
    {
        _ = LoadSupplierIngredientsAsync();
    }

    public async Task LoadSupplierIngredientsAsync()
    {
        if (SelectedSupplier == null)
        {
            SupplierIngredients.Clear();
            return;
        }

        var items = await ProcurementApiClient.Instance.GetSupplierIngredientsAsync(SelectedSupplier.SupplierId);
        SupplierIngredients = new ObservableCollection<SupplierIngredientDto>(items);
    }

    [RelayCommand]
    public async Task DeleteSupplierAsync()
    {
        if (SelectedSupplier == null) return;

        var result = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa Nhà Cung Cấp '{SelectedSupplier.SupplierName}'?\n(Bảng giá liên quan cũng sẽ bị xóa)",
            "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            bool ok = await ProcurementApiClient.Instance.DeleteSupplierAsync(SelectedSupplier.SupplierId);
            if (ok)
            {
                MessageBox.Show("Đã xóa nhà cung cấp thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadSuppliersAsync();
            }
            else
            {
                MessageBox.Show("Không thể xóa nhà cung cấp. Vui lòng thử lại sau.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteSupplierIngredientAsync()
    {
        if (SelectedSupplierIngredient == null) return;

        var result = MessageBox.Show(
            $"Bạn có chắc chắn muốn xóa nguyên liệu '{SelectedSupplierIngredient.IngredientName}' khỏi bảng giá NCC này?",
            "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            bool ok = await ProcurementApiClient.Instance.DeleteSupplierIngredientAsync(SelectedSupplierIngredient.SupplierIngredientId);
            if (ok)
            {
                await LoadSupplierIngredientsAsync();
                await LoadSuppliersAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
    #endregion

    #region Tab 2 Commands & Handlers (PO)
    [RelayCommand]
    public async Task LoadPurchaseOrdersAsync()
    {
        int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
        var list = await ProcurementApiClient.Instance.GetPurchaseOrdersAsync(branchId: null, status: "ALL");
        _allPurchaseOrders = list;

        PoOrderedCount = list.Count(p => p.Status == "ORDERED");
        PoPartiallyReceivedCount = list.Count(p => p.Status == "PARTIALLY_RECEIVED");
        PoCompletedCount = list.Count(p => p.Status == "COMPLETED");

        FilterPurchaseOrders();

        // Cập nhật danh sách đơn chờ nhận hàng ở Tab 3
        var pending = list.Where(p => p.Status == "ORDERED" || p.Status == "PARTIALLY_RECEIVED").ToList();
        PendingOrdersForReceipt = new ObservableCollection<PurchaseOrderDto>(pending);
        if (SelectedPendingPo == null && PendingOrdersForReceipt.Count > 0)
        {
            SelectedPendingPo = PendingOrdersForReceipt[0];
        }
    }

    partial void OnFilterPoStatusChanged(string value)
    {
        FilterPurchaseOrders();
    }

    partial void OnPoSearchKeywordChanged(string value)
    {
        FilterPurchaseOrders();
    }

    private void FilterPurchaseOrders()
    {
        var filtered = _allPurchaseOrders.AsEnumerable();

        if (FilterPoStatus != "ALL")
        {
            filtered = filtered.Where(p => p.Status == FilterPoStatus);
        }

        if (!string.IsNullOrWhiteSpace(PoSearchKeyword))
        {
            string kw = PoSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(p =>
                p.PoCode.ToLower().Contains(kw) ||
                p.SupplierName.ToLower().Contains(kw) ||
                p.BranchName.ToLower().Contains(kw));
        }

        PurchaseOrders = new ObservableCollection<PurchaseOrderDto>(filtered);
        if (SelectedPurchaseOrder == null && PurchaseOrders.Count > 0)
        {
            SelectedPurchaseOrder = PurchaseOrders[0];
        }
    }

    [RelayCommand]
    public async Task CancelPurchaseOrderAsync()
    {
        if (SelectedPurchaseOrder == null) return;
        if (SelectedPurchaseOrder.Status == "COMPLETED")
        {
            MessageBox.Show("Không thể hủy đơn đặt hàng đã hoàn tất tiếp nhận.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Bạn có chắc chắn muốn hủy đơn đặt hàng '{SelectedPurchaseOrder.PoCode}'?",
            "Xác nhận hủy đơn", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        IsLoading = true;
        try
        {
            bool ok = await ProcurementApiClient.Instance.UpdatePoStatusAsync(SelectedPurchaseOrder.PoId, "CANCELLED");
            if (ok)
            {
                MessageBox.Show("Đã hủy đơn đặt hàng thành công.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadPurchaseOrdersAsync();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
    #endregion

    #region Tab 3 Commands & Handlers (Goods Receipt QC)
    [RelayCommand]
    public async Task LoadGoodsReceiptsAsync()
    {
        var list = await ProcurementApiClient.Instance.GetGoodsReceiptsAsync();
        _allReceipts = list;
        FilterGoodsReceipts();
    }

    partial void OnReceiptSearchKeywordChanged(string value)
    {
        FilterGoodsReceipts();
    }

    private void FilterGoodsReceipts()
    {
        var filtered = _allReceipts.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(ReceiptSearchKeyword))
        {
            string kw = ReceiptSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(r =>
                r.ReceiptCode.ToLower().Contains(kw) ||
                r.PoCode.ToLower().Contains(kw) ||
                r.SupplierName.ToLower().Contains(kw) ||
                (r.DeliveryPerson != null && r.DeliveryPerson.ToLower().Contains(kw)));
        }

        GoodsReceipts = new ObservableCollection<GoodsReceiptDto>(filtered);
        if (SelectedReceipt == null && GoodsReceipts.Count > 0)
        {
            SelectedReceipt = GoodsReceipts[0];
        }
    }
    #endregion
}
