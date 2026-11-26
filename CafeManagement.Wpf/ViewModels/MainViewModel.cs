using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Views;
using CafeManagement.Wpf.Views.Products;
using CafeManagement.Wpf.Views.Recipes;
using CafeManagement.Wpf.Views.Customers;
using CafeManagement.Wpf.Views.Promotions;
using CafeManagement.Wpf.Views.Checkout;
using CafeManagement.Wpf.Views.POS;
using CafeManagement.Wpf.Views.WorkShifts;
using CafeManagement.Wpf.Services;
using CafeManagement.Wpf.Services.Authentication;
using CafeManagement.Wpf.Services.Http;
using CafeManagement.Core.DTOs.Products;
using CafeManagement.Core.DTOs.Customers;
using CafeManagement.Core.DTOs.Promotions;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Core.DTOs.WorkShifts;
using CafeManagement.Core.DTOs.KDS;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Core.DTOs.Employees;
using CafeManagement.Core.DTOs.Reports;
using CafeManagement.Wpf.ViewModels.KDS;
using CafeManagement.Wpf.ViewModels.Inventory;
using CafeManagement.Wpf.ViewModels.Employees;
using CafeManagement.Wpf.ViewModels.Reports;
using CafeManagement.Wpf.Views.Inventory;
using CafeManagement.Wpf.Views.Procurement;
using CafeManagement.Wpf.Views.Employees;
using CafeManagement.Wpf.Views.Orders;
using CafeManagement.Wpf.Services.Printing;
using CafeManagement.Wpf.Services.Offline;
using CafeManagement.Core.DTOs.Sync;
using System.Windows;

namespace CafeManagement.Wpf.ViewModels;

public class CartToppingItem
{
    public Topping Topping { get; set; } = null!;
    public int Quantity { get; set; } = 1;
}

public partial class CartItemViewModel : ObservableObject
{
    public Product Product { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
    public List<CartToppingItem> Toppings { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineTotal))]
    private int _quantity = 1;

    [ObservableProperty]
    private string _sugarLevel = "100%";

    [ObservableProperty]
    private string _iceLevel = "100%";

    [ObservableProperty]
    private string _note = string.Empty;

    public decimal UnitPrice => Product.BasePrice + Variant.ExtraPrice + Toppings.Sum(t => t.Topping.Price * t.Quantity);
    public decimal LineTotal => UnitPrice * Quantity;

    public string OptionsSummary
    {
        get
        {
            var parts = new List<string> { $"Size {Variant.Size}", $"{SugarLevel} đường", $"{IceLevel} đá" };
            if (Toppings.Count > 0)
            {
                parts.Add("+ " + string.Join(", ", Toppings.Select(t => t.Quantity > 1 ? $"{t.Topping.ToppingName} (x{t.Quantity})" : t.Topping.ToppingName)));
            }
            if (!string.IsNullOrWhiteSpace(Note))
            {
                parts.Add($"Ghi chú: {Note}");
            }
            return string.Join(" | ", parts);
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(OptionsSummary));
    }
}

public class HeldOrder
{
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime HeldAt { get; set; } = DateTime.Now;
    public List<CartItemViewModel> Items { get; set; } = [];
    public decimal TotalAmount => Items.Sum(i => i.LineTotal);
    public string Summary => string.Join(", ", Items.Select(i => $"{i.Quantity}x {i.Product.ProductName} ({i.Variant.Size})"));
}

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer = new();

    private static string GenerateOrderNumber() => $"HD{DateTime.Now:yyMMddHHmmss}-{Random.Shared.Next(100, 999)}";

    // 0. PHIÊN ĐĂNG NHẬP & PHÂN QUYỀN (RBAC)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccessPos))]
    [NotifyPropertyChangedFor(nameof(CanAccessKds))]
    [NotifyPropertyChangedFor(nameof(CanAccessInventory))]
    [NotifyPropertyChangedFor(nameof(CanAccessReport))]
    [NotifyPropertyChangedFor(nameof(CanAccessProduct))]
    [NotifyPropertyChangedFor(nameof(CanAccessCustomer))]
    [NotifyPropertyChangedFor(nameof(CanBrowseVouchers))]
    [NotifyPropertyChangedFor(nameof(CanAccessEmployee))]
    [NotifyPropertyChangedFor(nameof(CanAccessBranchManagement))]
    private UserSession _currentSession = new();

    public bool CanAccessPos => CurrentSession.CanAccessPos;
    public bool CanAccessKds => CurrentSession.CanAccessKds;
    public bool CanAccessInventory => CurrentSession.CanAccessInventory;
    public bool CanAccessReport => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessProduct => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessCustomer => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanBrowseVouchers => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessEmployee => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessBranchManagement => CurrentSession.IsOwnerOrAdmin;

    public event Action? RequestLogout;

    // 1. ĐIỀU HƯỚNG GIAO DIỆN (NAVIGATION)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPosView))]
    [NotifyPropertyChangedFor(nameof(IsKdsView))]
    [NotifyPropertyChangedFor(nameof(IsInventoryView))]
    [NotifyPropertyChangedFor(nameof(IsReportView))]
    [NotifyPropertyChangedFor(nameof(IsProductView))]
    [NotifyPropertyChangedFor(nameof(IsCustomerView))]
    [NotifyPropertyChangedFor(nameof(IsEmployeeView))]
    private string _currentView = "POS"; // "POS", "KDS", "INVENTORY", "PRODUCT", "REPORT", "CUSTOMER", "EMPLOYEE"

    public bool IsPosView => CurrentView == "POS";
    public bool IsKdsView => CurrentView == "KDS";
    public bool IsInventoryView => CurrentView == "INVENTORY";
    public bool IsReportView => CurrentView == "REPORT";
    public bool IsProductView => CurrentView == "PRODUCT";
    public bool IsCustomerView => CurrentView == "CUSTOMER";
    public bool IsEmployeeView => CurrentView == "EMPLOYEE";

    [ObservableProperty]
    private string _branchName = "Chi nhánh Quận 1 (BR01)";

    [ObservableProperty]
    private string _currentCashier = "Quản trị viên (admin)";

    [ObservableProperty]
    private string _currentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

    // 2. DỮ LIỆU BÁN HÀNG POS
    [ObservableProperty]
    private ObservableCollection<ProductCategory> _categories = [];

    [ObservableProperty]
    private ProductCategory? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<Product> _products = [];

    [ObservableProperty]
    private ObservableCollection<CartItemViewModel> _cartItems = [];

    [ObservableProperty]
    private ObservableCollection<HeldOrder> _heldOrders = [];

    [ObservableProperty]
    private int _heldOrdersCount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FinalAmount))]
    private decimal _subTotal = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FinalAmount))]
    private decimal _discountAmount = 0;

    public decimal FinalAmount => Math.Max(0, SubTotal - DiscountAmount);

    [ObservableProperty]
    private string _orderNumber = GenerateOrderNumber();

    // Khách hàng & Tích điểm & Khuyến mãi tại POS
    [ObservableProperty]
    private string _customerSearchPhone = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCustomer))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedCustomer))]
    private Customer? _selectedCustomer = null;

    public bool HasSelectedCustomer => SelectedCustomer != null;
    public bool HasNoSelectedCustomer => SelectedCustomer == null;

    [ObservableProperty]
    private bool _useCustomerPoints = false;

    [ObservableProperty]
    private int _pointsToUse = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPointsDiscount))]
    private decimal _pointsDiscountAmount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTierDiscount))]
    private decimal _tierDiscountAmount = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoucherDiscount))]
    private decimal _voucherDiscountAmount = 0;

    public bool HasTierDiscount => TierDiscountAmount > 0;
    public bool HasPointsDiscount => PointsDiscountAmount > 0;
    public bool HasVoucherDiscount => VoucherDiscountAmount > 0;

    [ObservableProperty]
    private string _voucherInputCode = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPromotion))]
    [NotifyPropertyChangedFor(nameof(HasNoSelectedPromotion))]
    private Promotion? _selectedPromotion = null;

    public bool HasSelectedPromotion => SelectedPromotion != null;
    public bool HasNoSelectedPromotion => SelectedPromotion == null;

    partial void OnUseCustomerPointsChanged(bool value) => UpdateTotals();

    // 2.2 TRẠNG THÁI MẠNG & ĐỒNG BỘ OFFLINE
    [ObservableProperty]
    private bool _isOnline = true;

    [ObservableProperty]
    private int _pendingSyncCount = 0;

    [ObservableProperty]
    private string _networkStatusText = "ONLINE";

    [ObservableProperty]
    private string _networkStatusBadgeColor = "#38A169";

    // 2.1 QUẢN LÝ CA LÀM VIỆC & KÉT TIỀN MẶT
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveShift))]
    [NotifyPropertyChangedFor(nameof(HasNoActiveShift))]
    [NotifyPropertyChangedFor(nameof(CurrentShiftBadge))]
    private WorkShift? _currentShift;

    public bool HasActiveShift => CurrentShift != null;
    public bool HasNoActiveShift => CurrentShift == null;
    public string CurrentShiftBadge => CurrentShift != null
        ? $"🟢 {CurrentShift.ShiftName} (#{CurrentShift.ShiftCode})"
        : "⏸ Chưa vào ca";

    // 3. DỮ LIỆU QUẦY PHA CHẾ (KDS BARISTA)
    [ObservableProperty]
    private ObservableCollection<KdsTicketViewModel> _kdsTickets = [];

    [ObservableProperty]
    private int _kdsWaitingCount = 0;

    [ObservableProperty]
    private int _kdsPreparingCount = 0;

    [ObservableProperty]
    private int _kdsReadyCount = 0;

    // 4. DỮ LIỆU KHO NGUYÊN LIỆU (INVENTORY)
    [ObservableProperty]
    private ObservableCollection<InventoryItemViewModel> _inventoryItems = [];

    [ObservableProperty]
    private ObservableCollection<InventoryTransactionItemViewModel> _inventoryTransactions = [];

    [ObservableProperty]
    private string _inventoryTransSearchKeyword = string.Empty;

    [ObservableProperty]
    private string _inventoryTransTypeFilter = "ALL";

    [ObservableProperty]
    private string _inventoryTransTypeFilterText = "Tất cả loại";

    private List<InventoryTransactionItemViewModel> _allInventoryTransactions = [];

    partial void OnInventoryTransSearchKeywordChanged(string value) => ApplyInventoryTransFilter();
    partial void OnInventoryTransTypeFilterChanged(string value) => ApplyInventoryTransFilter();

    [ObservableProperty]
    private int _lowStockCount = 0;

    // 5. DỮ LIỆU BÁO CÁO DOANH THU & THỐNG KÊ (REPORT)
    [ObservableProperty]
    private decimal _reportTotalRevenue = 0;

    [ObservableProperty]
    private int _reportTotalOrders = 0;

    [ObservableProperty]
    private int _reportTotalCups = 0;

    [ObservableProperty]
    private decimal _reportAverageOrderValue = 0;

    [ObservableProperty]
    private decimal _reportCashRevenue = 0;

    [ObservableProperty]
    private int _reportCashCount = 0;

    [ObservableProperty]
    private double _reportCashPercent = 50;

    [ObservableProperty]
    private decimal _reportQrRevenue = 0;

    [ObservableProperty]
    private int _reportQrCount = 0;

    [ObservableProperty]
    private double _reportQrPercent = 50;

    [ObservableProperty]
    private ObservableCollection<TopProductItemViewModel> _reportTopProducts = [];

    [ObservableProperty]
    private ObservableCollection<OrderReportItemViewModel> _reportRecentOrders = [];

    [ObservableProperty]
    private ObservableCollection<HourlySalesStatDto> _reportHourlySales = [];

    private List<HourlySalesStatDto> _overallHourlySales = [];

    [ObservableProperty]
    private ObservableCollection<DailyRevenueStatDto> _reportWeeklySales = [];

    [ObservableProperty]
    private ObservableCollection<string> _reportYAxisTicks = [];

    [ObservableProperty]
    private HourlySalesStatDto? _selectedHourlySale;

    [ObservableProperty]
    private DailyRevenueStatDto? _selectedDailySale;

    [ObservableProperty]
    private int _chartDays = 7;

    [ObservableProperty]
    private string _chartDaysFilterText = "7 Ngày";

    [ObservableProperty]
    private DateTime _chartFromDate = DateTime.Today.AddDays(-6);

    [ObservableProperty]
    private DateTime _chartToDate = DateTime.Today;

    [ObservableProperty]
    private bool _isCustomDateActive;

    [ObservableProperty]
    private string _heatmapTitle = "🔥 BIỂU ĐỒ NHIỆT KHUNG GIỜ (06h - 23h)";

    [ObservableProperty]
    private bool _isViewingSpecificDay;

    [RelayCommand]
    public void SelectHour(HourlySalesStatDto hourStat)
    {
        SelectedHourlySale = hourStat;
    }

    [RelayCommand]
    public void SelectDay(DailyRevenueStatDto dayStat)
    {
        if (dayStat == null) return;

        foreach (var d in ReportWeeklySales)
        {
            d.IsSelected = (d == dayStat);
        }
        var temp = new ObservableCollection<DailyRevenueStatDto>(ReportWeeklySales);
        ReportWeeklySales = temp;

        SelectedDailySale = dayStat;
        IsViewingSpecificDay = true;
        HeatmapTitle = $"🔥 BIỂU ĐỒ NHIỆT NGÀY: {dayStat.DayOfWeekName.ToUpper()} ({dayStat.DateFormatted})";

        ReportHourlySales = new ObservableCollection<HourlySalesStatDto>(dayStat.HourlySales);
        SelectedHourlySale = dayStat.HourlySales.OrderByDescending(h => h.TotalCups).FirstOrDefault(h => h.TotalCups > 0)
            ?? dayStat.HourlySales.FirstOrDefault();
    }

    [RelayCommand]
    public void ResetToOverallHeatmap()
    {
        IsViewingSpecificDay = false;
        SelectedDailySale = null;
        foreach (var d in ReportWeeklySales)
        {
            d.IsSelected = false;
        }
        var temp = new ObservableCollection<DailyRevenueStatDto>(ReportWeeklySales);
        ReportWeeklySales = temp;

        HeatmapTitle = "🔥 BIỂU ĐỒ NHIỆT KHUNG GIỜ TOÀN BỘ ĐỢT (06h - 23h)";
        ReportHourlySales = new ObservableCollection<HourlySalesStatDto>(_overallHourlySales);
        SelectedHourlySale = _overallHourlySales.OrderByDescending(h => h.TotalCups).FirstOrDefault(h => h.TotalCups > 0)
            ?? _overallHourlySales.FirstOrDefault();
    }

    [RelayCommand]
    public async Task ApplyChartDateFilterAsync()
    {
        if (ChartFromDate > ChartToDate)
        {
            (ChartFromDate, ChartToDate) = (ChartToDate, ChartFromDate);
        }

        IsCustomDateActive = true;
        ReportDateFilter = "CUSTOM";
        ReportDateFilterText = $"📅 {ChartFromDate:dd/MM} - {ChartToDate:dd/MM}";
        await LoadReportDataAsync();
    }

    [RelayCommand]
    public async Task FilterChartDaysAsync(string daysStr)
    {
        if (int.TryParse(daysStr, out int days) && days > 0)
        {
            ChartDays = days;
            ChartDaysFilterText = $"{days} Ngày";
            ChartToDate = DateTime.Today;
            ChartFromDate = DateTime.Today.AddDays(-(days - 1));
            IsCustomDateActive = true;
            ReportDateFilter = "CUSTOM";
            ReportDateFilterText = $"📅 {ChartFromDate:dd/MM} - {ChartToDate:dd/MM}";
            await LoadReportDataAsync();
        }
    }

    [ObservableProperty]
    private string _orderSearchKeyword = string.Empty;

    [ObservableProperty]
    private string _orderPaymentFilter = "ALL";

    [ObservableProperty]
    private string _orderPaymentFilterText = "Tất cả hình thức";

    private List<OrderReportItemViewModel> _allReportOrders = [];

    partial void OnOrderSearchKeywordChanged(string value) => ApplyOrderReportFilter();
    partial void OnOrderPaymentFilterChanged(string value) => ApplyOrderReportFilter();

    [ObservableProperty]
    private string _reportDateFilter = "ALL"; // TODAY, WEEK, MONTH, ALL

    [ObservableProperty]
    private string _reportDateFilterText = "Tất cả thời gian";

    [ObservableProperty]
    private string _reportBranchFilter = "CURRENT"; // CURRENT, ALL

    [ObservableProperty]
    private string _reportBranchFilterText = "Chi nhánh hiện tại";

    // 6. DỮ LIỆU QUẢN LÝ MÓN & BẢNG GIÁ (PRODUCT MANAGEMENT)
    [ObservableProperty]
    private ObservableCollection<ProductManagementItemViewModel> _managedProducts = [];

    [ObservableProperty]
    private ObservableCollection<ProductCategory> _managementCategories = [];

    [ObservableProperty]
    private ProductCategory? _selectedManagementCategory;

    public MainViewModel() : this(null)
    {
    }

    public MainViewModel(UserSession? session)
    {
        if (session != null)
        {
            CurrentSession = session;
            BranchName = session.BranchName;
            CurrentCashier = $"{session.FullName} ({session.RoleName})";
            CurrentView = session.DefaultView;
        }

        _ = LoadDataAsync();
        InitializeOfflineSync();

        // Timer quét đơn mới và cập nhật thời gian
        _timer.Interval = TimeSpan.FromSeconds(4);
        _timer.Tick += async (s, e) =>
        {
            CurrentTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            if (IsKdsView)
            {
                await RefreshKdsAsync();
            }
        };
        _timer.Start();
    }

    private void InitializeOfflineSync()
    {
        var syncMgr = OfflineSyncManager.Instance;
        IsOnline = syncMgr.IsOnline;
        PendingSyncCount = syncMgr.PendingSyncCount;
        NetworkStatusText = IsOnline ? "ONLINE" : "OFFLINE";
        NetworkStatusBadgeColor = IsOnline ? "#38A169" : "#E53E3E";

        syncMgr.ConnectionStatusChanged += isOnline =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsOnline = isOnline;
                NetworkStatusText = isOnline ? "ONLINE" : "OFFLINE";
                NetworkStatusBadgeColor = isOnline ? "#38A169" : "#E53E3E";
            });
        };

        syncMgr.PendingCountChanged += count =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                PendingSyncCount = count;
            });
        };

        syncMgr.ConflictDetected += conflict =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                var dlg = new SyncConflictDialog(conflict);
                if (Application.Current?.MainWindow != null) dlg.Owner = Application.Current.MainWindow;
                if (dlg.ShowDialog() == true)
                {
                    await OfflineSyncManager.Instance.ResolveConflictAsync(conflict.LocalId, conflict.ServerOrderId, dlg.ResultChoice);
                    MessageBox.Show($"Đã xử lý xung đột đơn #{conflict.OrderNumber} thành công ({dlg.ResultChoice})!", "Đồng Bộ", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        };

        syncMgr.StartMonitoring();
    }

    [RelayCommand]
    public async Task ManualSyncAsync()
    {
        bool online = await OfflineSyncManager.Instance.CheckConnectionAsync();
        if (!online)
        {
            MessageBox.Show(
                $"🔴 Hiện tại máy POS đang OFFLINE (Không thể kết nối đến máy chủ {ApiClient.Instance.BaseUrl}).\n\n" +
                $"• Số đơn đang chờ đồng bộ: {PendingSyncCount} đơn\n\n" +
                $"Ứng dụng vẫn cho phép bán hàng tiền mặt bình thường. Hệ thống sẽ tự động gửi đơn khi có mạng.",
                "Trạng Thái Mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (PendingSyncCount == 0)
        {
            MessageBox.Show($"🟢 Kết nối máy chủ ({ApiClient.Instance.BaseUrl}) HOÀN HẢO!\n• Tất cả đơn hàng đã được đồng bộ 100%.", "Đồng Bộ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        int before = PendingSyncCount;
        await OfflineSyncManager.Instance.TriggerSyncAsync();
        MessageBox.Show($"🎉 Đã kiểm tra và tiến hành đồng bộ {before} đơn hàng lên máy chủ!", "Đồng Bộ Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void Logout()
    {
        RequestLogout?.Invoke();
    }

    // ==================== CÁC HÀM ĐIỀU HƯỚNG MENU ====================
    [RelayCommand]
    public void SwitchToPos()
    {
        CurrentView = "POS";
    }

    [RelayCommand]
    public async Task SwitchToKds()
    {
        CurrentView = "KDS";
        await RefreshKdsAsync();
    }

    [RelayCommand]
    public async Task SwitchToInventory()
    {
        CurrentView = "INVENTORY";
        await RefreshInventoryAsync();
    }

    [RelayCommand]
    public async Task SwitchToReport()
    {
        CurrentView = "REPORT";
        await LoadReportDataAsync();
    }

    [RelayCommand]
    public async Task SwitchToProduct()
    {
        CurrentView = "PRODUCT";
        await LoadManagementProductsAsync();
    }

    [RelayCommand]
    public async Task SwitchToCustomer()
    {
        if (!CanAccessCustomer)
        {
            MessageBox.Show("Bạn không có quyền truy cập vào phân hệ Quản lý Khách hàng & Khuyến mãi!\nChỉ Quản trị viên và Quản lý chi nhánh mới có quyền thao tác.", "Từ chối truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CurrentView = "CUSTOMER";
        await LoadCustomerManagementDataAsync();
    }

    [RelayCommand]
    public async Task SwitchToEmployee()
    {
        if (!CanAccessEmployee)
        {
            MessageBox.Show("Bạn không có quyền truy cập vào phân hệ Quản lý Nhân sự & Ca làm việc!\nChỉ Quản trị viên và Quản lý chi nhánh mới có quyền thao tác.", "Từ chối truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        CurrentView = "EMPLOYEE";
        await LoadEmployeeManagementDataAsync();
    }

    // ==================== LOGIC BÁN HÀNG POS ====================
    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            var cats = await ProductApiClient.Instance.GetCategoriesAsync(activeOnly: true);
            Categories = new ObservableCollection<ProductCategory>(cats);
            await FilterProductsAsync(null);

            // Kiểm tra ca làm việc đang mở của chi nhánh
            int curBranchId = CurrentSession.BranchId;
            CurrentShift = await ShiftApiClient.Instance.GetCurrentOpenShiftAsync(curBranchId);

            // Tự động lưu cache Menu, Toppings & Nguyên liệu cho chế độ Offline
            if (cats != null && cats.Count > 0)
            {
                var prods = await ProductApiClient.Instance.GetProductsAsync(null, activeOnly: true);
                var toppings = await ProductApiClient.Instance.GetToppingsAsync(activeOnly: true);
                List<BranchInventoryDto>? invs = null;
                try
                {
                    invs = await InventoryApiClient.Instance.GetBranchInventoryAsync(curBranchId > 0 ? curBranchId : 1);
                }
                catch { }

                OfflineStorageService.Instance.SaveMenuCache(cats, prods, toppings, invs);
            }
        }
        catch (Exception ex)
        {
            // Mất kết nối: Tự động tải từ Cache cục bộ
            var cache = OfflineStorageService.Instance.GetMenuCache();
            if (cache != null && cache.Categories.Count > 0)
            {
                Categories = new ObservableCollection<ProductCategory>(cache.Categories);
                Products = new ObservableCollection<Product>(cache.Products);
            }
            else
            {
                MessageBox.Show($"Không thể kết nối máy chủ và chưa có bộ nhớ đệm Offline: {ex.Message}", "Cảnh Báo Mạng", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    [RelayCommand]
    public async Task FilterProductsAsync(ProductCategory? category)
    {
        SelectedCategory = category;

        // Nếu offline, lọc trực tiếp từ bộ nhớ đệm không đợi HTTP
        if (!OfflineSyncManager.Instance.IsOnline)
        {
            var cache = OfflineStorageService.Instance.GetMenuCache();
            if (cache != null)
            {
                var filtered = (category == null || category.CategoryId == 0)
                    ? cache.Products
                    : cache.Products.Where(p => p.CategoryId == category.CategoryId).ToList();
                Products = new ObservableCollection<Product>(filtered);
            }
            return;
        }

        try
        {
            var list = await ProductApiClient.Instance.GetProductsAsync(category?.CategoryId, activeOnly: true);
            Products = new ObservableCollection<Product>(list);
        }
        catch
        {
            // Mất mạng: Lọc từ Cache cục bộ
            var cache = OfflineStorageService.Instance.GetMenuCache();
            if (cache != null)
            {
                var filtered = (category == null || category.CategoryId == 0)
                    ? cache.Products
                    : cache.Products.Where(p => p.CategoryId == category.CategoryId).ToList();
                Products = new ObservableCollection<Product>(filtered);
            }
        }
    }

    [RelayCommand]
    public async Task OpenCustomizeDialogAsync(Product product)
    {
        try
        {
            List<Topping> toppings;
            List<ToppingRecommendationDto> recommendations = [];

            if (!OfflineSyncManager.Instance.IsOnline)
            {
                toppings = OfflineStorageService.Instance.GetCachedToppings();
            }
            else
            {
                toppings = await ProductApiClient.Instance.GetToppingsAsync(activeOnly: true);
                try
                {
                    recommendations = await ProductApiClient.Instance.GetRecommendedToppingsAsync(product.ProductId, 3);
                }
                catch { }
            }

            var vm = new CustomizeProductViewModel(product, toppings, null, recommendations);

            var dialog = new CustomizeProductWindow(vm);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                var selectedVariant = vm.SelectedVariant ?? product.Variants.FirstOrDefault() ?? new ProductVariant
                {
                    ProductId = product.ProductId,
                    Size = "M",
                    Temperature = "ICED",
                    ExtraPrice = 0
                };

                var selectedToppings = vm.Toppings
                    .Where(t => t.IsSelected)
                    .Select(t => new CartToppingItem { Topping = t.Topping, Quantity = t.Quantity })
                    .ToList();

                var cartItem = new CartItemViewModel
                {
                    Product = product,
                    Variant = selectedVariant,
                    Quantity = vm.Quantity,
                    SugarLevel = vm.SelectedSugar,
                    IceLevel = vm.SelectedIce,
                    Note = vm.Note,
                    Toppings = selectedToppings
                };

                CartItems.Add(cartItem);
                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở tùy chọn món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task EditCartItemAsync(CartItemViewModel item)
    {
        try
        {
            List<Topping> toppings;
            List<ToppingRecommendationDto> recommendations = [];

            if (!OfflineSyncManager.Instance.IsOnline)
            {
                toppings = OfflineStorageService.Instance.GetCachedToppings();
            }
            else
            {
                toppings = await ProductApiClient.Instance.GetToppingsAsync(activeOnly: true);
                try
                {
                    recommendations = await ProductApiClient.Instance.GetRecommendedToppingsAsync(item.Product.ProductId, 3);
                }
                catch { }
            }

            var vm = new CustomizeProductViewModel(item.Product, toppings, item, recommendations);

            var dialog = new CustomizeProductWindow(vm);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                item.Variant = vm.SelectedVariant ?? item.Variant;
                item.Quantity = vm.Quantity;
                item.SugarLevel = vm.SelectedSugar;
                item.IceLevel = vm.SelectedIce;
                item.Note = vm.Note;
                item.Toppings = vm.Toppings
                    .Where(t => t.IsSelected)
                    .Select(t => new CartToppingItem { Topping = t.Topping, Quantity = t.Quantity })
                    .ToList();

                item.Refresh();
                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi chỉnh sửa món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void IncreaseQuantity(CartItemViewModel item)
    {
        item.Quantity++;
        UpdateTotals();
    }

    [RelayCommand]
    public void DecreaseQuantity(CartItemViewModel item)
    {
        if (item.Quantity > 1)
        {
            item.Quantity--;
        }
        else
        {
            CartItems.Remove(item);
        }
        UpdateTotals();
    }

    [RelayCommand]
    public void RemoveCartItem(CartItemViewModel item)
    {
        CartItems.Remove(item);
        UpdateTotals();
    }

    [RelayCommand]
    public void ClearCart()
    {
        CartItems.Clear();
        UpdateTotals();
        OrderNumber = GenerateOrderNumber();
    }

    [RelayCommand]
    public void HoldOrder()
    {
        if (CartItems.Count == 0)
        {
            MessageBox.Show("Giỏ hàng đang trống, không thể tạm giữ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var held = new HeldOrder
        {
            OrderNumber = OrderNumber,
            HeldAt = DateTime.Now,
            Items = CartItems.ToList()
        };

        HeldOrders.Add(held);
        UpdateHeldOrdersCount();

        CartItems.Clear();
        UpdateTotals();
        OrderNumber = GenerateOrderNumber();

        MessageBox.Show($"Đã tạm giữ đơn #{held.OrderNumber} thành công!\nBạn có thể tiếp tục phục vụ khách hàng tiếp theo.", 
            "Tạm giữ thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void OpenHeldOrders()
    {
        if (HeldOrders.Count == 0)
        {
            MessageBox.Show("Hiện tại không có đơn hàng nào đang tạm giữ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new HeldOrdersWindow(this);
        if (Application.Current?.MainWindow != null)
        {
            dialog.Owner = Application.Current.MainWindow;
        }

        if (dialog.ShowDialog() == true && dialog.SelectedOrderToRestore != null)
        {
            var restore = dialog.SelectedOrderToRestore;
            HeldOrders.Remove(restore);
            UpdateHeldOrdersCount();

            CartItems = new ObservableCollection<CartItemViewModel>(restore.Items);
            OrderNumber = restore.OrderNumber;
            UpdateTotals();
        }
    }

    public void UpdateHeldOrdersCount()
    {
        HeldOrdersCount = HeldOrders.Count;
    }

    // THANH TOÁN (MỞ HỘP THOẠI VÀ TỰ ĐỘNG TRỪ KHO NGUYÊN LIỆU THEO CÔNG THỨC)
    [RelayCommand]
    public async Task CheckoutAsync()
    {
        try
        {
            if (CartItems.Count == 0)
            {
                MessageBox.Show("Giỏ hàng đang trống! Vui lòng chọn món trước khi thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CurrentShift == null)
            {
                var openShiftPrompt = MessageBox.Show(
                    "⚠️ Quầy thu ngân chưa mở ca làm việc (Shift In)!\n\n" +
                    "Bạn có muốn Mở Ca ngay bây giờ để ghi nhận doanh thu và bàn giao quỹ tiền mặt không?",
                    "Chưa mở ca làm việc", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (openShiftPrompt == MessageBoxResult.Yes)
                {
                    await OpenShiftDialogAsync();
                    if (CurrentShift == null) return;
                }
            }

            var checkoutVm = new CheckoutViewModel(OrderNumber, FinalAmount);
            var dialog = new CheckoutWindow(checkoutVm);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true)
            {
                int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
                int cashierId = CurrentSession.UserId > 0 ? CurrentSession.UserId : 1;

                var req = new CreateOrderRequest
                {
                    BranchId = branchId,
                    CashierUserId = cashierId,
                    ShiftId = CurrentShift?.ShiftId,
                    CustomerId = SelectedCustomer?.CustomerId,
                    CustomerPhone = SelectedCustomer?.PhoneNumber,
                    CustomerPointsUsed = UseCustomerPoints ? PointsToUse : 0,
                    PromotionCode = SelectedPromotion?.PromotionCode,
                    OrderType = "TAKE_AWAY",
                    PaymentMethod = checkoutVm.PaymentMethod,
                    CustomerGivenAmount = checkoutVm.CustomerGivenAmount,
                    SubTotal = SubTotal,
                    DiscountAmount = DiscountAmount,
                    FinalAmount = FinalAmount,
                    Items = CartItems.Select(c => new OrderItemRequest
                    {
                        ProductId = c.Product.ProductId,
                        VariantId = c.Variant.VariantId > 0 ? c.Variant.VariantId : 1,
                        Quantity = c.Quantity,
                        UnitPrice = c.UnitPrice,
                        Size = c.Variant.Size ?? "M",
                        Sugar = c.SugarLevel,
                        Ice = c.IceLevel,
                        Note = c.Note,
                        OptionsSummary = c.OptionsSummary,
                        Toppings = c.Toppings.Select(t => new OrderItemToppingRequest
                        {
                            ToppingId = t.Topping.ToppingId,
                            Quantity = t.Quantity,
                            UnitPrice = t.Topping.Price
                        }).ToList()
                    }).ToList()
                };

                OrderDto? createdOrder = null;
                bool isOffline = false;

                if (OfflineSyncManager.Instance.IsOnline)
                {
                    try
                    {
                        createdOrder = await OrderApiClient.Instance.CreateOrderAsync(req);
                    }
                    catch
                    {
                        createdOrder = null;
                    }
                }

                if (createdOrder == null)
                {
                    // Chuyển sang lưu đơn Offline cục bộ
                    var offlineRecord = OfflineSyncManager.Instance.CreateOfflineOrder(req);
                    isOffline = true;
                    createdOrder = new OrderDto
                    {
                        OrderId = 0,
                        OrderNumber = offlineRecord.OrderNumber,
                        BranchId = req.BranchId,
                        BranchName = BranchName,
                        CashierUserId = req.CashierUserId,
                        CashierName = CurrentCashier,
                        Status = "OFFLINE_PENDING",
                        SubTotal = req.SubTotal,
                        DiscountAmount = req.DiscountAmount,
                        FinalAmount = req.FinalAmount,
                        PaymentMethod = req.PaymentMethod,
                        CreatedAt = offlineRecord.CreatedAt
                    };
                }

                // Cập nhật ca làm việc cục bộ
                if (CurrentShift != null)
                {
                    if (checkoutVm.PaymentMethod == "CASH")
                    {
                        CurrentShift.CashSales += FinalAmount;
                    }
                    else
                    {
                        CurrentShift.VietQrSales += FinalAmount;
                    }
                    CurrentShift.TotalSales += FinalAmount;
                    CurrentShift.TotalOrders += 1;
                    CurrentShift.ExpectedClosingCash = CurrentShift.OpeningCash + CurrentShift.CashSales;
                }

                string methodStr = checkoutVm.PaymentMethod == "CASH" ? "Tiền mặt" : "Chuyển khoản VietQR";
                string changeStr = checkoutVm.PaymentMethod == "CASH" ? $"\nTiền thối lại: {checkoutVm.ChangeAmount:N0} đ" : "";
                string promoInfoStr = SelectedPromotion != null ? $"\n\n🎟️ ƯU ĐÃI: {SelectedPromotion.PromotionName} (-{VoucherDiscountAmount:N0} đ)" : "";
                string customerInfoStr = SelectedCustomer != null ? $"\n\n👤 KHÁCH HÀNG: {SelectedCustomer.FullName} ({SelectedCustomer.PhoneNumber})" + (createdOrder.PointsEarned > 0 ? $"\n• Điểm tích lũy mới: +{createdOrder.PointsEarned} điểm" : "") : "";

                string successTitle = isOffline ? "⚡ ĐÃ LƯU ĐƠN CỤC BỘ (OFFLINE)" : "🎉 THANH TOÁN THÀNH CÔNG!";
                string offlineNotice = isOffline 
                    ? $"\n\n🔴 [MẤT KẾT NỐI MÁY CHỦ] Đơn đã lưu vào hàng đợi cục bộ!\n• Đang chờ đồng bộ: {OfflineSyncManager.Instance.PendingSyncCount} đơn\n• Hệ thống sẽ tự động đồng bộ lên máy chủ khi có mạng lại."
                    : "\n\n⚡ Đã tự động trừ nguyên liệu trong kho!";

                var promptPrint = MessageBox.Show(
                    $"{successTitle}\n\n" +
                    $"• Mã đơn: #{createdOrder.OrderNumber}\n" +
                    $"• Tổng thanh toán: {FinalAmount:N0} đ\n" +
                    $"• Hình thức: {methodStr}{changeStr}\n" +
                    $"• Biên nhận: REC_{createdOrder.OrderNumber}" +
                    promoInfoStr + customerInfoStr + 
                    offlineNotice + "\n\n" +
                    $"Bạn có muốn mở & in Phiếu Pha Chế (Barista Ticket) ngay không?",
                    isOffline ? "Đã Lưu Đơn Offline & In Phiếu" : "Hoàn tất đơn hàng & In Phiếu", 
                    MessageBoxButton.YesNo, 
                    isOffline ? MessageBoxImage.Warning : MessageBoxImage.Information);

                if (promptPrint == MessageBoxResult.Yes)
                {
                    var ticketWnd = new KitchenTicketWindow(createdOrder);
                    if (Application.Current?.MainWindow != null)
                    {
                        ticketWnd.Owner = Application.Current.MainWindow;
                    }
                    ticketWnd.ShowDialog();
                }

                SelectedCustomer = null;
                SelectedPromotion = null;
                UseCustomerPoints = false;
                PointsToUse = 0;
                PointsDiscountAmount = 0;
                TierDiscountAmount = 0;
                VoucherDiscountAmount = 0;
                CustomerSearchPhone = "";
                VoucherInputCode = "";
                ClearCart();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình thanh toán: {ex.Message}\n\nChi tiết:\n{ex}", "Lỗi Thanh Toán", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task SearchCustomerAsync()
    {
        if (!OfflineSyncManager.Instance.IsOnline)
        {
            MessageBox.Show("Ứng dụng đang ở chế độ OFFLINE (mất kết nối mạng).\nTính năng tra cứu thông tin khách hàng và tích điểm thành viên tạm thời không khả dụng cho đến khi có mạng trở lại.", "Chế Độ Offline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            string phone = CustomerSearchPhone.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại khách hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cust = await CustomerApiClient.Instance.GetCustomerByPhoneAsync(phone);
            if (cust != null)
            {
                SelectedCustomer = cust;
                UseCustomerPoints = false;
                CustomerSearchPhone = "";
                UpdateTotals();
            }
            else
            {
                var res = MessageBox.Show($"Khách hàng có SĐT '{phone}' chưa đăng ký thành viên.\nBạn có muốn đăng ký thành viên mới ngay không?", 
                    "Khách hàng mới", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    var dlg = new AddCustomerWindow(phone);
                    if (dlg.ShowDialog() == true && dlg.CreatedCustomer != null)
                    {
                        var created = await CustomerApiClient.Instance.CreateCustomerAsync(new CustomerCreateRequest
                        {
                            PhoneNumber = dlg.CreatedCustomer.PhoneNumber,
                            FullName = dlg.CreatedCustomer.FullName
                        });

                        if (created != null)
                        {
                            SelectedCustomer = created;
                            UseCustomerPoints = false;
                            CustomerSearchPhone = "";
                            UpdateTotals();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tìm kiếm khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void RemoveCustomer()
    {
        SelectedCustomer = null;
        UseCustomerPoints = false;
        PointsToUse = 0;
        PointsDiscountAmount = 0;
        TierDiscountAmount = 0;
        UpdateTotals();
    }

    [RelayCommand]
    public async Task ApplyVoucherAsync()
    {
        if (!OfflineSyncManager.Instance.IsOnline)
        {
            MessageBox.Show("Ứng dụng đang ở chế độ OFFLINE (mất kết nối mạng).\nTính năng áp dụng mã Voucher trực tuyến chỉ hỗ trợ khi có kết nối Internet.", "Chế Độ Offline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            string code = VoucherInputCode.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Vui lòng nhập mã Voucher / Khuyến mãi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var promo = await PromotionApiClient.Instance.GetPromotionByCodeAsync(code);
            if (promo == null)
            {
                MessageBox.Show($"Mã khuyến mãi '{code}' không tồn tại trong hệ thống!", "Không hợp lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!promo.IsActive || DateTime.UtcNow < promo.StartDate || DateTime.UtcNow > promo.EndDate)
            {
                MessageBox.Show($"Mã khuyến mãi '{code}' đã hết hạn hoặc tạm ngưng áp dụng!", "Không khả dụng", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (promo.UsageLimit.HasValue && promo.UsedCount >= promo.UsageLimit.Value)
            {
                MessageBox.Show($"Mã khuyến mãi '{code}' đã hết số lượt sử dụng!", "Hết lượt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SubTotal < promo.MinOrderAmount)
            {
                MessageBox.Show($"Đơn hàng cần đạt tối thiểu {promo.MinOrderAmount:N0} đ để áp dụng mã '{code}'!\n(Hiện tại: {SubTotal:N0} đ)", "Chưa đủ điều kiện", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedPromotion = promo;
            VoucherInputCode = "";
            UpdateTotals();

            MessageBox.Show($"🎉 Áp dụng thành công mã ưu đãi '{promo.PromotionCode}'!\n• {promo.PromotionName}\n• {promo.DiscountDisplay}", "Áp Dụng Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi áp dụng voucher: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenSelectVoucherDialogAsync()
    {
        try
        {
            if (!CanBrowseVouchers)
            {
                MessageBox.Show("Thu ngân không được phép xem trước danh mục mã ưu đãi hệ thống.\nVui lòng yêu cầu khách hàng cung cấp mã ưu đãi để nhập trực tiếp!", "Hạn chế quyền", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var activePromos = await PromotionApiClient.Instance.GetPromotionsAsync(activeOnly: true);

            if (activePromos.Count == 0)
            {
                MessageBox.Show("Hiện tại không có mã khuyến mãi nào đang kích hoạt.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SelectPromotionWindow(activePromos);
            if (dlg.ShowDialog() == true && dlg.SelectedPromotion != null)
            {
                var promo = dlg.SelectedPromotion;
                if (SubTotal < promo.MinOrderAmount)
                {
                    MessageBox.Show($"Đơn hàng cần đạt tối thiểu {promo.MinOrderAmount:N0} đ để áp dụng mã '{promo.PromotionCode}'!\n(Hiện tại: {SubTotal:N0} đ)", "Chưa đủ điều kiện", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SelectedPromotion = promo;
                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi chọn voucher: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void RemoveVoucher()
    {
        SelectedPromotion = null;
        VoucherDiscountAmount = 0;
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        SubTotal = CartItems.Sum(c => c.LineTotal);

        // 1. Chiết khấu theo hạng thành viên
        if (SelectedCustomer != null && SelectedCustomer.DiscountPercent > 0)
        {
            TierDiscountAmount = SubTotal * (SelectedCustomer.DiscountPercent / 100);
        }
        else
        {
            TierDiscountAmount = 0;
        }

        // 2. Chiết khấu từ đổi điểm thưởng (1 điểm = 100 đ)
        if (SelectedCustomer != null && UseCustomerPoints)
        {
            decimal remainingAfterTier = Math.Max(0, SubTotal - TierDiscountAmount);
            int maxPointsAllowed = (int)(remainingAfterTier / 100);
            PointsToUse = Math.Max(0, Math.Min(SelectedCustomer.TotalPoints, maxPointsAllowed));
            PointsDiscountAmount = PointsToUse * 100;
        }
        else
        {
            PointsToUse = 0;
            PointsDiscountAmount = 0;
        }

        // 3. Chiết khấu từ Voucher
        if (SelectedPromotion != null)
        {
            if (SubTotal < SelectedPromotion.MinOrderAmount)
            {
                VoucherDiscountAmount = 0;
            }
            else if (SelectedPromotion.DiscountType == "PERCENT")
            {
                decimal vDisc = SubTotal * (SelectedPromotion.DiscountValue / 100);
                if (SelectedPromotion.MaxDiscountAmount.HasValue && vDisc > SelectedPromotion.MaxDiscountAmount.Value)
                {
                    vDisc = SelectedPromotion.MaxDiscountAmount.Value;
                }
                VoucherDiscountAmount = vDisc;
            }
            else if (SelectedPromotion.DiscountType == "FIXED_AMOUNT")
            {
                VoucherDiscountAmount = Math.Min(SubTotal, SelectedPromotion.DiscountValue);
            }
            else if (SelectedPromotion.DiscountType == "BUY1_GET1")
            {
                int totalCups = CartItems.Sum(c => c.Quantity);
                if (totalCups >= 2)
                {
                    decimal minCupPrice = CartItems.Min(c => c.LineTotal / c.Quantity);
                    VoucherDiscountAmount = minCupPrice;
                }
                else
                {
                    VoucherDiscountAmount = 0;
                }
            }
        }
        else
        {
            VoucherDiscountAmount = 0;
        }

        DiscountAmount = TierDiscountAmount + PointsDiscountAmount + VoucherDiscountAmount;
        OnPropertyChanged(nameof(FinalAmount));
    }

    // ==================== LOGIC QUẦY PHA CHẾ (KDS BARISTA) ====================
    [RelayCommand]
    public async Task RefreshKdsAsync()
    {
        try
        {
            int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;

            var ticketsDto = await KdsApiClient.Instance.GetActiveTicketsAsync(branchId);

            var tickets = new List<KdsTicketViewModel>();
            foreach (var ord in ticketsDto)
            {
                var ticket = new KdsTicketViewModel
                {
                    OrderId = ord.OrderId,
                    OrderNumber = ord.OrderNumber,
                    OrderType = ord.OrderType,
                    OrderStatus = ord.OrderStatus,
                    CreatedAt = ord.CreatedAt.ToLocalTime()
                };

                foreach (var itm in ord.Items)
                {
                    ticket.Items.Add(new KdsItemViewModel
                    {
                        OrderItemId = itm.OrderItemId,
                        OrderId = ord.OrderId,
                        ProductName = itm.ProductName,
                        Size = itm.Size,
                        Quantity = itm.Quantity,
                        OptionsSummary = itm.OptionsSummary,
                        Note = itm.Note,
                        Status = itm.Status
                    });
                }

                tickets.Add(ticket);
            }

            KdsTickets = new ObservableCollection<KdsTicketViewModel>(tickets);

            KdsWaitingCount = tickets.Count(t => t.OrderStatus == "PAID");
            KdsPreparingCount = tickets.Count(t => t.OrderStatus == "PREPARING");
            KdsReadyCount = tickets.Count(t => t.OrderStatus == "READY");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi tải KDS: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task StartPreparingItemAsync(KdsItemViewModel item)
    {
        try
        {
            item.Status = "PREPARING";
            bool success = await KdsApiClient.Instance.UpdateItemStatusAsync(item.OrderItemId, "PREPARING");
            if (success)
            {
                await RefreshKdsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật món: {ex.Message}", "Lỗi KDS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task CompleteItemAsync(KdsItemViewModel item)
    {
        try
        {
            item.Status = "READY";
            bool success = await KdsApiClient.Instance.UpdateItemStatusAsync(item.OrderItemId, "READY");
            if (success)
            {
                await RefreshKdsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật món: {ex.Message}", "Lỗi KDS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ServeOrderAsync(KdsTicketViewModel ticket)
    {
        try
        {
            bool success = await KdsApiClient.Instance.CompleteOrderAsync(ticket.OrderId);
            if (success)
            {
                MessageBox.Show($"🎉 Đã bàn giao đơn hàng #{ticket.OrderNumber} cho khách thành công!", 
                    "Bàn Giao Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshKdsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi bàn giao đơn: {ex.Message}", "Lỗi KDS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== LOGIC KHO NGUYÊN LIỆU (INVENTORY) ====================
    [RelayCommand]
    public async Task RefreshInventoryAsync()
    {
        int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;

        // Nếu đang Offline, đọc ngay lập tức từ bộ nhớ đệm
        if (!OfflineSyncManager.Instance.IsOnline)
        {
            var cachedStocks = OfflineStorageService.Instance.GetCachedInventories();
            if (cachedStocks != null && cachedStocks.Count > 0)
            {
                var items = cachedStocks.Select(s => new InventoryItemViewModel
                {
                    BranchInventoryId = s.BranchInventoryId,
                    IngredientId = s.IngredientId,
                    IngredientCode = s.IngredientCode,
                    IngredientName = s.IngredientName,
                    BaseUom = s.BaseUom,
                    QuantityOnHand = s.QuantityOnHand,
                    MinStockAlert = s.MinStockAlert
                }).ToList();

                InventoryItems = new ObservableCollection<InventoryItemViewModel>(items);
                LowStockCount = items.Count(i => i.IsLowStock);
                return;
            }
        }

        try
        {
            var stocks = await InventoryApiClient.Instance.GetBranchInventoryAsync(branchId);
            var items = stocks.Select(s => new InventoryItemViewModel
            {
                BranchInventoryId = s.BranchInventoryId,
                IngredientId = s.IngredientId,
                IngredientCode = s.IngredientCode,
                IngredientName = s.IngredientName,
                BaseUom = s.BaseUom,
                QuantityOnHand = s.QuantityOnHand,
                MinStockAlert = s.MinStockAlert
            }).ToList();

            InventoryItems = new ObservableCollection<InventoryItemViewModel>(items);
            LowStockCount = items.Count(i => i.IsLowStock);

            // Tự động lưu cache kho nguyên liệu
            OfflineStorageService.Instance.SaveCachedInventories(stocks);

            var trans = await InventoryApiClient.Instance.GetTransactionsAsync(branchId, 500);
            _allInventoryTransactions = trans.Select(t => new InventoryTransactionItemViewModel
            {
                TransactionId = t.TransactionId,
                BranchId = t.BranchId,
                IngredientId = t.IngredientId,
                TransactionType = t.TransactionType,
                CreatedAt = t.CreatedAt.ToLocalTime(),
                IngredientName = t.IngredientName,
                QuantityChange = t.QuantityChange,
                BalanceAfter = t.BalanceAfter,
                BaseUom = t.BaseUom,
                ReferenceId = t.ReferenceId,
                Note = t.Note ?? ""
            }).ToList();

            ApplyInventoryTransFilter();
        }
        catch (Exception ex)
        {
            var cachedStocks = OfflineStorageService.Instance.GetCachedInventories();
            if (cachedStocks != null && cachedStocks.Count > 0)
            {
                var items = cachedStocks.Select(s => new InventoryItemViewModel
                {
                    BranchInventoryId = s.BranchInventoryId,
                    IngredientId = s.IngredientId,
                    IngredientCode = s.IngredientCode,
                    IngredientName = s.IngredientName,
                    BaseUom = s.BaseUom,
                    QuantityOnHand = s.QuantityOnHand,
                    MinStockAlert = s.MinStockAlert
                }).ToList();

                InventoryItems = new ObservableCollection<InventoryItemViewModel>(items);
                LowStockCount = items.Count(i => i.IsLowStock);
            }
            else
            {
                MessageBox.Show($"Lỗi tải dữ liệu kho: {ex.Message}", "Lỗi Kho", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Mở hộp thoại Quản lý Bộ Nhớ Đệm Tồn Kho Offline
    [RelayCommand]
    public async Task OpenOfflineInventoryDialogAsync()
    {
        try
        {
            var dlg = new OfflineInventoryWindow();
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }
            dlg.ShowDialog();

            // Tải lại kho nếu đang ở màn hình kho
            if (IsInventoryView)
            {
                await RefreshInventoryAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở bộ nhớ đệm nguyên liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ApplyInventoryTransFilter()
    {
        var filtered = _allInventoryTransactions.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(InventoryTransSearchKeyword))
        {
            string kw = InventoryTransSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(t =>
                (t.IngredientName?.ToLower().Contains(kw) == true) ||
                (t.ReferenceId?.ToLower().Contains(kw) == true) ||
                (t.Note?.ToLower().Contains(kw) == true) ||
                (t.TransactionTypeBadge?.ToLower().Contains(kw) == true));
        }

        if (!string.IsNullOrEmpty(InventoryTransTypeFilter) && InventoryTransTypeFilter != "ALL")
        {
            filtered = filtered.Where(t => t.TransactionType == InventoryTransTypeFilter);
        }

        InventoryTransactions = new ObservableCollection<InventoryTransactionItemViewModel>(filtered.ToList());
    }

    [RelayCommand]
    public void FilterInventoryTransByType(string type)
    {
        InventoryTransTypeFilter = type;
        InventoryTransTypeFilterText = type switch
        {
            "GOODS_RECEIPT" => "📥 Nhập kho",
            "SALE_CONSUMPTION" => "☕ Bán hàng",
            "STOCK_ADJUSTMENT" => "⚖️ Điều chỉnh",
            "INITIAL_STOCK" => "🌱 Tồn ban đầu",
            "RECEIPT_CANCELLED" => "❌ Đã hủy",
            _ => "Tất cả loại"
        };
        ApplyInventoryTransFilter();
    }

    // Hủy phiếu nhập kho (hoàn tác số lượng nếu nhập sai và ghi nhận lịch sử)
    [RelayCommand]
    public async Task CancelGoodsReceiptAsync(InventoryTransactionItemViewModel item)
    {
        try
        {
            if (item == null || !item.CanCancel) return;

            var confirm = MessageBox.Show(
                $"Bạn có chắc chắn muốn HỦY PHIẾU NHẬP #{item.ReferenceId}?\n\n" +
                $"👉 Nguyên liệu: {item.IngredientName}\n" +
                $"👉 Số lượng thu hồi lại: -{item.QuantityChange:N1} {item.BaseUom}\n" +
                $"👉 Ghi chú phiếu gốc: {item.Note}\n\n" +
                "Sau khi hủy, số lượng tồn kho sẽ được trừ lại tương ứng và hệ thống sẽ lưu lại vết lịch sử hủy phiếu vào Thẻ kho.",
                "Xác Nhận Hủy Phiếu Nhập Kho",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            bool success = await InventoryApiClient.Instance.CancelReceiptAsync(item.TransactionId);
            if (success)
            {
                MessageBox.Show($"✅ Đã hủy phiếu #{item.ReferenceId} và thu hồi -{item.QuantityChange:N1} {item.BaseUom} {item.IngredientName} thành công!",
                    "Đã Hủy Phiếu Nhập", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshInventoryAsync();
            }
            else
            {
                MessageBox.Show("Không thể hủy phiếu nhập kho này trên máy chủ API!", "Lỗi Hủy Phiếu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi hủy phiếu nhập kho: {ex.Message}", "Lỗi Hủy Phiếu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Mở Quản lý mua hàng, nhà cung cấp & tiếp nhận hàng hóa QC (Bước 18.0)
    [RelayCommand]
    public void OpenProcurementManagement()
    {
        var window = new ProcurementManagementWindow(CurrentSession);
        if (Application.Current?.MainWindow != null)
        {
            window.Owner = Application.Current.MainWindow;
        }
        window.ShowDialog();
        _ = RefreshInventoryAsync();
    }

    // Mở Quản lý điều chuyển nguyên liệu liên chi nhánh
    [RelayCommand]
    public void OpenStockTransferManagement()
    {
        var window = new StockTransferManagementWindow(CurrentSession);
        if (Application.Current?.MainWindow != null)
        {
            window.Owner = Application.Current.MainWindow;
        }
        window.ShowDialog();
        _ = RefreshInventoryAsync();
    }

    // Mở Quản lý hệ thống chi nhánh chuỗi (Chỉ dành cho Admin)
    [RelayCommand]
    public void OpenBranchManagement()
    {
        if (!CanAccessBranchManagement)
        {
            MessageBox.Show("Chỉ Quản trị viên (Chủ chuỗi / Admin) mới có quyền quản lý và thiết lập chi nhánh!", "Từ chối truy cập", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new CafeManagement.Wpf.Views.Branches.BranchManagementWindow();
        if (Application.Current?.MainWindow != null)
        {
            dlg.Owner = Application.Current.MainWindow;
        }
        dlg.ShowDialog();
    }

    // Mở hộp thoại Nhập kho nhanh
    [RelayCommand]
    public async Task OpenImportStockDialogAsync()
    {
        try
        {
            var allIngredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: true);
            var dialog = new ImportStockWindow(allIngredients);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true && dialog.SelectedIngredient != null)
            {
                int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
                var req = new ImportStockRequest
                {
                    BranchId = branchId,
                    IngredientId = dialog.SelectedIngredient.IngredientId,
                    Quantity = dialog.ImportQuantity,
                    Note = dialog.Note
                };

                var updated = await InventoryApiClient.Instance.ImportStockAsync(req);
                if (updated != null)
                {
                    MessageBox.Show($"🎉 Đã nhập kho thành công +{dialog.ImportQuantity:N0} {dialog.SelectedIngredient.BaseUom} {dialog.SelectedIngredient.IngredientName}!", 
                        "Nhập Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshInventoryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nhập kho: {ex.Message}", "Lỗi Nhập Kho", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Mở hộp thoại Thêm nguyên liệu mới vào kho
    [RelayCommand]
    public async Task OpenAddIngredientDialogAsync()
    {
        try
        {
            var dlg = new IngredientEditWindow(null);
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true && dlg.IngredientToSave != null)
            {
                int currentBranchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
                var req = new IngredientCreateRequest
                {
                    IngredientCode = dlg.IngredientToSave.IngredientCode,
                    IngredientName = dlg.IngredientToSave.IngredientName,
                    BaseUom = dlg.IngredientToSave.BaseUom,
                    MinStockAlert = dlg.MinStockAlert,
                    InitialStock = dlg.InitialStock,
                    CurrentBranchId = currentBranchId,
                    CreatorName = CurrentCashier
                };

                var created = await InventoryApiClient.Instance.CreateIngredientAsync(req);
                if (created != null)
                {
                    MessageBox.Show($"🎉 Đã thêm thành công nguyên liệu '{created.IngredientName}' vào hệ thống kho!",
                        "Thêm Nguyên Liệu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshInventoryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm nguyên liệu: {ex.Message}", "Lỗi Thêm Nguyên Liệu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Mở hộp thoại Sửa thông tin nguyên liệu
    [RelayCommand]
    public async Task OpenEditIngredientDialogAsync(InventoryItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var ing = new Ingredient
            {
                IngredientId = item.IngredientId,
                IngredientCode = item.IngredientCode,
                IngredientName = item.IngredientName,
                BaseUom = item.BaseUom,
                MinStockAlert = item.MinStockAlert
            };

            var dlg = new IngredientEditWindow(ing, item.MinStockAlert);
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true && dlg.IngredientToSave != null)
            {
                var req = new IngredientUpdateRequest
                {
                    IngredientName = dlg.IngredientToSave.IngredientName,
                    BaseUom = dlg.IngredientToSave.BaseUom,
                    MinStockAlert = dlg.MinStockAlert
                };

                var updated = await InventoryApiClient.Instance.UpdateIngredientAsync(item.IngredientId, req);
                if (updated != null)
                {
                    MessageBox.Show($"🎉 Đã cập nhật thành công nguyên liệu '{updated.IngredientName}'!",
                        "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshInventoryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật nguyên liệu: {ex.Message}", "Lỗi Cập Nhật", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Mở hộp thoại Điều chỉnh / Kiểm kê tồn kho & lưu lịch sử
    [RelayCommand]
    public async Task OpenAdjustStockDialogAsync(InventoryItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var dlg = new AdjustStockWindow(item);
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true)
            {
                int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
                decimal oldQty = item.QuantityOnHand;
                decimal newQty = dlg.NewQuantity;
                decimal diff = newQty - oldQty;

                var req = new AdjustStockRequest
                {
                    BranchId = branchId,
                    IngredientId = item.IngredientId,
                    NewQuantity = newQty,
                    Reason = dlg.Reason,
                    Note = dlg.Note,
                    PerformedBy = CurrentCashier
                };

                var updated = await InventoryApiClient.Instance.AdjustStockAsync(req);
                if (updated != null)
                {
                    MessageBox.Show($"🎉 Đã điều chỉnh kiểm kê tồn kho cho '{item.IngredientName}' thành công!\n\n" +
                                    $"• Số lượng cũ: {oldQty:N1} {item.BaseUom}\n" +
                                    $"• Số lượng mới: {newQty:N1} {item.BaseUom}\n" +
                                    $"• Chênh lệch: {(diff >= 0 ? "+" : "")}{diff:N1} {item.BaseUom}\n\n" +
                                    $"⚡ Lịch sử đã được ghi nhận vào thẻ kho.",
                                    "Điều Chỉnh Tồn Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await RefreshInventoryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi điều chỉnh tồn kho: {ex.Message}", "Lỗi Điều Chỉnh", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== LOGIC BÁO CÁO DOANH THU & THỐNG KÊ (REPORT) ====================
    [RelayCommand]
    public async Task FilterReportByDate(string filter)
    {
        ReportDateFilter = filter;
        IsCustomDateActive = false;

        switch (filter)
        {
            case "TODAY":
                ReportDateFilterText = "Hôm nay";
                ChartFromDate = DateTime.Today;
                ChartToDate = DateTime.Today;
                ChartDays = 1;
                break;
            case "WEEK":
                ReportDateFilterText = "7 ngày qua";
                ChartFromDate = DateTime.Today.AddDays(-6);
                ChartToDate = DateTime.Today;
                ChartDays = 7;
                break;
            case "MONTH":
                ReportDateFilterText = "Tháng này";
                ChartFromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                ChartToDate = DateTime.Today;
                ChartDays = (DateTime.Today - ChartFromDate).Days + 1;
                break;
            default:
                ReportDateFilterText = "Tất cả thời gian";
                ChartFromDate = DateTime.Today.AddDays(-29);
                ChartToDate = DateTime.Today;
                ChartDays = 30;
                break;
        }

        await LoadReportDataAsync();
    }

    [RelayCommand]
    public async Task FilterReportByBranch(string filter)
    {
        ReportBranchFilter = filter;
        ReportBranchFilterText = filter == "ALL" ? "Toàn chuỗi (Tất cả chi nhánh)" : "Chi nhánh hiện tại";
        await LoadReportDataAsync();
    }

    [RelayCommand]
    public async Task LoadReportDataAsync()
    {
        try
        {
            int? branchId = (ReportBranchFilter == "CURRENT" && CurrentSession.BranchId > 0) ? CurrentSession.BranchId : null;
            DateTime? fDate = IsCustomDateActive ? ChartFromDate : null;
            DateTime? tDate = IsCustomDateActive ? ChartToDate : null;

            var report = await ReportApiClient.Instance.GetDashboardReportAsync(branchId, ReportDateFilter, ChartDays, fDate, tDate);
            if (report != null)
            {
                ReportTotalOrders = report.TotalOrders;
                ReportTotalRevenue = report.TotalRevenue;
                ReportTotalCups = report.TotalCups;
                ReportAverageOrderValue = report.AverageOrderValue;

                ReportCashRevenue = report.CashRevenue;
                ReportCashCount = report.CashCount;
                ReportCashPercent = report.CashPercent;

                ReportQrRevenue = report.QrRevenue;
                ReportQrCount = report.QrCount;
                ReportQrPercent = report.QrPercent;

                ReportTopProducts = new ObservableCollection<TopProductItemViewModel>(
                    report.TopProducts.Select(p => new TopProductItemViewModel
                    {
                        Rank = p.Rank,
                        ProductName = p.ProductName,
                        TotalQuantity = p.TotalQuantity,
                        TotalRevenue = p.TotalRevenue,
                        PercentOfTotal = p.PercentOfTotal
                    })
                );

                _allReportOrders = report.Orders.Select(o => new OrderReportItemViewModel
                {
                    OrderId = o.OrderId,
                    OrderNumber = o.OrderNumber,
                    CreatedAt = o.CreatedAt.ToLocalTime(),
                    CashierName = o.CashierName,
                    BranchName = o.BranchName,
                    PaymentMethod = o.PaymentMethod,
                    ItemsSummary = o.ItemsSummary,
                    FinalAmount = o.FinalAmount,
                    OrderStatus = o.OrderStatus,
                    RefundedAmount = o.RefundedAmount
                }).ToList();

                _overallHourlySales = report.HourlySales;
                ReportHourlySales = new ObservableCollection<HourlySalesStatDto>(report.HourlySales);
                ReportWeeklySales = new ObservableCollection<DailyRevenueStatDto>(report.WeeklySales);
                ReportYAxisTicks = new ObservableCollection<string>(report.YAxisTickLabels);

                IsViewingSpecificDay = false;
                SelectedDailySale = null;
                HeatmapTitle = "🔥 BIỂU ĐỒ NHIỆT KHUNG GIỜ TOÀN BỘ ĐỢT (06h - 23h)";

                SelectedHourlySale = report.HourlySales.OrderByDescending(h => h.TotalCups).FirstOrDefault(h => h.TotalCups > 0)
                    ?? report.HourlySales.FirstOrDefault();

                ApplyOrderReportFilter();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu báo cáo: {ex.Message}", "Lỗi Báo Cáo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ApplyOrderReportFilter()
    {
        var filtered = _allReportOrders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(OrderSearchKeyword))
        {
            string kw = OrderSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(o =>
                (o.OrderNumber?.ToLower().Contains(kw) == true) ||
                (o.CashierName?.ToLower().Contains(kw) == true) ||
                (o.ItemsSummary?.ToLower().Contains(kw) == true) ||
                (o.BranchName?.ToLower().Contains(kw) == true));
        }

        if (!string.IsNullOrEmpty(OrderPaymentFilter) && OrderPaymentFilter != "ALL")
        {
            filtered = filtered.Where(o => o.PaymentMethod == OrderPaymentFilter);
        }

        ReportRecentOrders = new ObservableCollection<OrderReportItemViewModel>(filtered.ToList());
    }

    [RelayCommand]
    public void FilterOrderPayment(string payment)
    {
        OrderPaymentFilter = payment;
        OrderPaymentFilterText = payment switch
        {
            "CASH" => "💵 Tiền mặt",
            "VIETQR" => "📲 VietQR",
            _ => "Tất cả hình thức"
        };
        ApplyOrderReportFilter();
    }

    [RelayCommand]
    public async Task OpenRefundDialogAsync(OrderReportItemViewModel? orderItem)
    {
        if (orderItem == null) return;
        if (!orderItem.CanRefund)
        {
            MessageBox.Show("Đơn hàng này đã được hoàn tiền hoặc đã bị hủy, không thể thực hiện hoàn tiền!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var fullOrder = await OrderApiClient.Instance.GetOrderByIdAsync(orderItem.OrderId);
            var dlg = new RefundOrderWindow(orderItem, fullOrder, CurrentSession.UserId, CurrentSession.FullName);
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true && dlg.ResultRequest != null)
            {
                dlg.ResultRequest.ProcessedByUserId = CurrentSession.UserId;
                dlg.ResultRequest.ProcessedByName = CurrentSession.FullName;

                var res = await RefundApiClient.Instance.CreateRefundAsync(dlg.ResultRequest);
                if (res != null)
                {
                    MessageBox.Show($"🎉 Đã hoàn tiền thành công {res.RefundAmount:N0} đ cho đơn hàng {orderItem.OrderNumber}!\n• Hình thức hoàn: {res.RefundMethod}\n• Lý do: {res.RefundReason}", 
                                    "Hoàn Tiền Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                    await LoadReportDataAsync();

                    if (CurrentSession.BranchId > 0)
                    {
                        CurrentShift = await ShiftApiClient.Instance.GetCurrentOpenShiftAsync(CurrentSession.BranchId);
                    }
                }
                else
                {
                    MessageBox.Show("Thực hiện hoàn tiền thất bại!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thực hiện hoàn tiền: {ex.Message}", "Lỗi Hoàn Tiền", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenKitchenTicketAsync(OrderReportItemViewModel? orderItem)
    {
        if (orderItem == null) return;

        try
        {
            var fullOrder = await OrderApiClient.Instance.GetOrderByIdAsync(orderItem.OrderId);
            if (fullOrder == null)
            {
                MessageBox.Show("Không tìm thấy thông tin chi tiết đơn hàng để in phiếu pha chế!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ticketWnd = new KitchenTicketWindow(fullOrder);
            if (Application.Current?.MainWindow != null)
            {
                ticketWnd.Owner = Application.Current.MainWindow;
            }
            ticketWnd.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở phiếu pha chế: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 6. QUẢN LÝ MÓN NƯỚC, GIÁ & CÔNG THỨC PHA CHẾ (BOM) ====================
    private List<ProductManagementItemViewModel> _allManagedProducts = [];

    [RelayCommand]
    public async Task LoadManagementProductsAsync()
    {
        try
        {
            var cats = await ProductApiClient.Instance.GetCategoriesAsync(activeOnly: false);
            ManagementCategories = new ObservableCollection<ProductCategory>(cats);

            var products = await ProductApiClient.Instance.GetProductsAsync(null, activeOnly: false);

            _allManagedProducts = products.Select(p => new ProductManagementItemViewModel
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "Khác",
                BasePrice = p.BasePrice,
                Description = p.Description ?? "",
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                VariantsDisplay = string.Join(" | ", p.Variants.OrderBy(v => v.Size).Select(v => $"Size {v.Size}: +{v.ExtraPrice:N0} đ")),
                Product = p
            }).ToList();

            FilterManagementProducts(SelectedManagementCategory);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh mục món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void FilterManagementProducts(ProductCategory? cat)
    {
        SelectedManagementCategory = cat;
        if (cat == null)
        {
            ManagedProducts = new ObservableCollection<ProductManagementItemViewModel>(_allManagedProducts);
        }
        else
        {
            ManagedProducts = new ObservableCollection<ProductManagementItemViewModel>(
                _allManagedProducts.Where(p => p.Product.CategoryId == cat.CategoryId));
        }
    }

    [RelayCommand]
    public async Task OpenAddCategoryDialogAsync()
    {
        try
        {
            var dlg = new CategoryEditWindow();
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true)
            {
                await LoadDataAsync();
                await LoadManagementProductsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenToppingManagementDialogAsync()
    {
        try
        {
            var dlg = new ToppingManagementWindow();
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            dlg.ShowDialog();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi quản lý Topping: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenAddProductDialogAsync()
    {
        try
        {
            var categories = await ProductApiClient.Instance.GetCategoriesAsync(activeOnly: false);
            var dlg = new ProductEditWindow(categories);
            if (dlg.ShowDialog() == true && dlg.ProductToSave != null)
            {
                var req = new ProductCreateRequest
                {
                    CategoryId = dlg.ProductToSave.CategoryId,
                    ProductCode = dlg.ProductToSave.ProductCode,
                    ProductName = dlg.ProductToSave.ProductName,
                    Description = dlg.ProductToSave.Description,
                    BasePrice = dlg.ProductToSave.BasePrice,
                    ImageUrl = dlg.ProductToSave.ImageUrl,
                    ExtraPriceSizeL = dlg.ExtraPriceSizeL,
                    IsActive = dlg.ProductToSave.IsActive
                };
                await ProductApiClient.Instance.CreateProductAsync(req);

                await LoadDataAsync();
                await LoadManagementProductsAsync();

                MessageBox.Show($"🎉 Đã thêm thành công món '{dlg.ProductToSave.ProductName}' vào thực đơn!", 
                    "Thêm Món Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenEditProductDialogAsync(ProductManagementItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var categories = await ProductApiClient.Instance.GetCategoriesAsync(activeOnly: false);
            var product = item.Product;

            var dlg = new ProductEditWindow(categories, product);
            if (dlg.ShowDialog() == true)
            {
                var req = new ProductUpdateRequest
                {
                    CategoryId = dlg.ProductToSave?.CategoryId ?? product.CategoryId,
                    ProductName = dlg.ProductToSave?.ProductName ?? product.ProductName,
                    Description = dlg.ProductToSave?.Description ?? product.Description,
                    BasePrice = dlg.ProductToSave?.BasePrice ?? product.BasePrice,
                    ImageUrl = dlg.ProductToSave?.ImageUrl ?? product.ImageUrl,
                    ExtraPriceSizeL = dlg.ExtraPriceSizeL,
                    IsActive = dlg.ProductToSave?.IsActive ?? product.IsActive
                };
                await ProductApiClient.Instance.UpdateProductAsync(product.ProductId, req);

                await LoadDataAsync();
                await LoadManagementProductsAsync();

                MessageBox.Show($"🎉 Đã cập nhật thành công món '{product.ProductName}'!", 
                    "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cập nhật món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenRecipeConfigDialogAsync(ProductManagementItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var product = item.Product;
            var ingredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: false);
            var dlg = new RecipeConfigWindow(product, ingredients);
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi cấu hình công thức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ToggleProductActiveAsync(ProductManagementItemViewModel? item)
    {
        if (item == null) return;
        try
        {
            var ok = await ProductApiClient.Instance.ToggleProductActiveAsync(item.ProductId);
            if (ok)
            {
                item.IsActive = !item.IsActive;
                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đổi trạng thái món: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== 7. QUẢN LÝ KHÁCH HÀNG & CHƯƠNG TRÌNH KHUYẾN MÃI (S04) ====================
    [ObservableProperty]
    private ObservableCollection<Customer> _managedCustomers = [];

    [ObservableProperty]
    private ObservableCollection<Promotion> _managedPromotions = [];

    [ObservableProperty]
    private string _customerSearchKeyword = string.Empty;

    [ObservableProperty]
    private string _customerTierFilter = "ALL"; // ALL, BRONZE, SILVER, GOLD, DIAMOND

    [ObservableProperty]
    private int _totalCustomerCount = 0;

    [ObservableProperty]
    private int _totalActivePromotions = 0;

    private List<Customer> _allCustomers = [];

    [RelayCommand]
    public async Task LoadCustomerManagementDataAsync()
    {
        try
        {
            var custs = await CustomerApiClient.Instance.GetCustomersAsync();
            _allCustomers = custs;
            TotalCustomerCount = custs.Count;

            ApplyCustomerFilter();

            var promos = await PromotionApiClient.Instance.GetPromotionsAsync();
            ManagedPromotions = new ObservableCollection<Promotion>(promos);
            TotalActivePromotions = promos.Count(p => p.IsActive && p.EndDate >= DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu khách hàng & ưu đãi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ApplyCustomerFilter()
    {
        var filtered = _allCustomers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(CustomerSearchKeyword))
        {
            string kw = CustomerSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(c =>
                c.FullName.ToLower().Contains(kw) ||
                c.PhoneNumber.Contains(kw));
        }

        if (!string.IsNullOrEmpty(CustomerTierFilter) && CustomerTierFilter != "ALL")
        {
            filtered = filtered.Where(c => c.MembershipTier == CustomerTierFilter);
        }

        ManagedCustomers = new ObservableCollection<Customer>(filtered.ToList());
    }

    partial void OnCustomerSearchKeywordChanged(string value) => ApplyCustomerFilter();

    [RelayCommand]
    public void FilterCustomerByTier(string tier)
    {
        CustomerTierFilter = tier;
        ApplyCustomerFilter();
    }

    [RelayCommand]
    public async Task OpenAddCustomerDialogAsync()
    {
        try
        {
            var dlg = new AddCustomerWindow("");
            if (dlg.ShowDialog() == true && dlg.CreatedCustomer != null)
            {
                var created = await CustomerApiClient.Instance.CreateCustomerAsync(new CustomerCreateRequest
                {
                    PhoneNumber = dlg.CreatedCustomer.PhoneNumber,
                    FullName = dlg.CreatedCustomer.FullName
                });

                if (created != null)
                {
                    await LoadCustomerManagementDataAsync();
                    MessageBox.Show($"🎉 Đã đăng ký thành viên cho '{created.FullName}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenAdjustCustomerPointsDialogAsync(Customer? customer)
    {
        if (customer == null) return;
        try
        {
            var dlg = new AdjustCustomerPointsWindow(customer);
            if (dlg.ShowDialog() == true)
            {
                var updated = await CustomerApiClient.Instance.AdjustPointsAsync(new AdjustCustomerPointsRequest
                {
                    CustomerId = customer.CustomerId,
                    PointAdjustment = dlg.PointDelta,
                    Reason = "Điều chỉnh điểm từ giao diện quản lý"
                });

                if (updated != null)
                {
                    await LoadCustomerManagementDataAsync();
                    MessageBox.Show($"🎉 Đã điều chỉnh điểm & hạng thành viên cho '{updated.FullName}' thành công!\n• Điểm hiện tại: {updated.TotalPoints} điểm\n• Hạng: {updated.MembershipTier}", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi điều chỉnh điểm: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenAddPromotionDialogAsync()
    {
        try
        {
            var dlg = new PromotionEditWindow(null);
            if (dlg.ShowDialog() == true && dlg.PromoToSave != null)
            {
                var p = dlg.PromoToSave;
                var req = new PromotionCreateUpdateRequest
                {
                    PromotionCode = p.PromotionCode,
                    PromotionName = p.PromotionName,
                    DiscountType = p.DiscountType,
                    DiscountValue = p.DiscountValue,
                    MaxDiscountAmount = p.MaxDiscountAmount,
                    MinOrderAmount = p.MinOrderAmount,
                    UsageLimit = p.UsageLimit,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive
                };

                var created = await PromotionApiClient.Instance.CreatePromotionAsync(req);
                if (created != null)
                {
                    await LoadCustomerManagementDataAsync();
                    MessageBox.Show($"🎉 Đã tạo voucher '{created.PromotionCode}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm voucher: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenEditPromotionDialogAsync(Promotion? promo)
    {
        if (promo == null) return;
        try
        {
            var dlg = new PromotionEditWindow(promo);
            if (dlg.ShowDialog() == true && dlg.PromoToSave != null)
            {
                var p = dlg.PromoToSave;
                var req = new PromotionCreateUpdateRequest
                {
                    PromotionCode = p.PromotionCode,
                    PromotionName = p.PromotionName,
                    DiscountType = p.DiscountType,
                    DiscountValue = p.DiscountValue,
                    MaxDiscountAmount = p.MaxDiscountAmount,
                    MinOrderAmount = p.MinOrderAmount,
                    UsageLimit = p.UsageLimit,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive
                };

                var updated = await PromotionApiClient.Instance.UpdatePromotionAsync(promo.PromotionId, req);
                if (updated != null)
                {
                    await LoadCustomerManagementDataAsync();
                    MessageBox.Show($"🎉 Đã cập nhật voucher '{updated.PromotionCode}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi sửa voucher: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task TogglePromotionActiveAsync(Promotion? promo)
    {
        if (promo == null) return;
        try
        {
            bool success = await PromotionApiClient.Instance.TogglePromotionActiveAsync(promo.PromotionId);
            if (success)
            {
                await LoadCustomerManagementDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đổi trạng thái voucher: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==================== QUẢN LÝ CA LÀM VIỆC & KẾT TOÁN QUỸ (Z-REPORT) ====================
    [RelayCommand]
    public async Task OpenShiftDialogAsync()
    {
        try
        {
            if (HasActiveShift)
            {
                MessageBox.Show($"Bạn đang trong ca '{CurrentShift!.ShiftName}' (#{CurrentShift.ShiftCode})!\nVui lòng kết ca hiện tại trước khi mở ca mới.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int branchId = CurrentSession.BranchId;
            string branchName = CurrentSession.BranchName;
            int cashierId = CurrentSession.UserId;
            string cashierName = CurrentSession.FullName;

            var dlg = new ShiftOpenWindow(branchId, branchName, cashierId, cashierName);
            if (dlg.ShowDialog() == true && dlg.CreatedShift != null)
            {
                var req = new OpenShiftRequest
                {
                    BranchId = branchId,
                    CashierUserId = cashierId,
                    CashierName = cashierName,
                    ShiftName = dlg.CreatedShift.ShiftName,
                    OpeningCash = dlg.CreatedShift.OpeningCash,
                    Notes = dlg.CreatedShift.Notes
                };

                var opened = await ShiftApiClient.Instance.OpenShiftAsync(req);
                if (opened != null)
                {
                    CurrentShift = opened;

                    MessageBox.Show($"🎉 Đã mở ca '{CurrentShift.ShiftName}' (#{CurrentShift.ShiftCode}) thành công!\n" +
                                    $"• Tiền đầu ca: {CurrentShift.OpeningCash:N0} đ\n" +
                                    $"• Thu ngân: {CurrentShift.CashierName}\n" +
                                    $"Chúc bạn một ca làm việc thuận lợi!",
                                    "Mở Ca Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở ca làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task CloseShiftDialogAsync()
    {
        try
        {
            if (CurrentShift == null)
            {
                MessageBox.Show("Hiện tại không có ca làm việc nào đang mở!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new ShiftCloseWindow(CurrentShift);
            if (dlg.ShowDialog() == true)
            {
                var req = new CloseShiftRequest
                {
                    ShiftId = CurrentShift.ShiftId,
                    ActualClosingCash = dlg.ActualCash,
                    Notes = dlg.ClosingNotes
                };

                var closedShift = await ShiftApiClient.Instance.CloseShiftAsync(req);
                if (closedShift != null)
                {
                    CurrentShift = null;

                    // Mở Phiếu kết ca Z-Report
                    var rptDlg = new ZReportWindow(closedShift, CurrentSession.BranchName);
                    rptDlg.ShowDialog();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đóng ca làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ViewCurrentShiftStatus()
    {
        if (CurrentShift == null)
        {
            var res = MessageBox.Show("Quầy chưa có ca làm việc nào đang mở.\nBạn có muốn mở ca làm việc ngay bây giờ không?", "Chưa mở ca", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                _ = OpenShiftDialogAsync();
            }
            return;
        }

        MessageBox.Show(
            $"📋 THÔNG TIN CA LÀM VIỆC HIỆN TẠI:\n\n" +
            $"• Mã ca: {CurrentShift.ShiftCode}\n" +
            $"• Tên ca: {CurrentShift.ShiftName}\n" +
            $"• Thu ngân: {CurrentShift.CashierName}\n" +
            $"• Bắt đầu: {CurrentShift.StartTime.ToLocalTime():dd/MM/yyyy HH:mm}\n" +
            $"• Tiền lẻ đầu ca: {CurrentShift.OpeningCash:N0} đ\n" +
            $"• Doanh thu Tiền mặt: {CurrentShift.CashSales:N0} đ\n" +
            $"• Doanh thu VietQR: {CurrentShift.VietQrSales:N0} đ\n" +
            $"• TỔNG DOANH THU: {CurrentShift.TotalSales:N0} đ ({CurrentShift.TotalOrders} đơn)\n" +
            $"• Tiền mặt dự kiến trong két: {CurrentShift.ExpectedClosingCash:N0} đ",
            "Trạng Thái Ca Làm Việc", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    public void ViewShiftZReport(WorkShift? shift)
    {
        if (shift == null) return;
        string branchName = shift.Branch?.BranchName ?? CurrentSession.BranchName;
        var dlg = new ZReportWindow(shift, branchName);
        dlg.ShowDialog();
    }

    // ==================== QUẢN LÝ NHÂN SỰ & PHÂN QUYỀN (S01) ====================
    [ObservableProperty]
    private ObservableCollection<EmployeeListItemViewModel> _managedEmployees = [];

    [ObservableProperty]
    private ObservableCollection<WorkShift> _managedShifts = [];

    [ObservableProperty]
    private string _employeeSearchKeyword = string.Empty;

    [ObservableProperty]
    private string _employeeRoleFilter = "ALL";

    [ObservableProperty]
    private string _shiftStatusFilter = "ALL";

    [ObservableProperty]
    private int _totalEmployeeCount = 0;

    [ObservableProperty]
    private int _totalShiftCount = 0;

    private List<EmployeeListItemViewModel> _allEmployees = [];
    private List<WorkShift> _allShifts = [];

    partial void OnEmployeeSearchKeywordChanged(string value) => ApplyEmployeeFilter();
    partial void OnEmployeeRoleFilterChanged(string value) => ApplyEmployeeFilter();
    partial void OnShiftStatusFilterChanged(string value) => ApplyShiftFilter();

    [RelayCommand]
    public async Task LoadEmployeeManagementDataAsync()
    {
        try
        {
            var empDtos = await EmployeeApiClient.Instance.GetEmployeesAsync();
            var list = empDtos.Select(emp => new EmployeeListItemViewModel
            {
                EmployeeId = emp.EmployeeId,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                Phone = emp.Phone,
                Email = emp.Email,
                CitizenId = emp.CitizenId,
                Status = emp.Status,
                HireDate = emp.HireDate,
                UserId = emp.UserId,
                Username = emp.Username,
                RoleId = emp.RoleId,
                RoleCode = emp.RoleCode,
                RoleName = string.IsNullOrWhiteSpace(emp.RoleName) ? "Chưa phân vai trò" : emp.RoleName,
                BranchId = emp.BranchId,
                BranchName = string.IsNullOrWhiteSpace(emp.BranchName) ? "Chưa gán chi nhánh" : emp.BranchName
            }).ToList();

            _allEmployees = list;
            TotalEmployeeCount = list.Count;
            ApplyEmployeeFilter();

            var shifts = await ShiftApiClient.Instance.GetShiftsAsync();
            _allShifts = shifts;
            TotalShiftCount = shifts.Count;
            ApplyShiftFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu nhân sự & ca làm việc: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void ApplyEmployeeFilter()
    {
        var filtered = _allEmployees.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(EmployeeSearchKeyword))
        {
            string kw = EmployeeSearchKeyword.Trim().ToLower();
            filtered = filtered.Where(e => e.FullName.ToLower().Contains(kw) || e.Phone.Contains(kw) || e.EmployeeCode.ToLower().Contains(kw) || e.Username.ToLower().Contains(kw));
        }

        if (EmployeeRoleFilter != "ALL")
        {
            filtered = filtered.Where(e => e.RoleCode == EmployeeRoleFilter);
        }

        ManagedEmployees = new ObservableCollection<EmployeeListItemViewModel>(filtered);
    }

    public void ApplyShiftFilter()
    {
        var filtered = _allShifts.AsEnumerable();

        if (ShiftStatusFilter != "ALL")
        {
            filtered = filtered.Where(s => s.Status == ShiftStatusFilter);
        }

        ManagedShifts = new ObservableCollection<WorkShift>(filtered);
    }

    [RelayCommand]
    public void FilterEmployeeRole(string roleCode)
    {
        EmployeeRoleFilter = roleCode;
    }

    [RelayCommand]
    public void FilterShiftStatus(string status)
    {
        ShiftStatusFilter = status;
    }

    [RelayCommand]
    public void OpenStaffingAnalyticsDialog()
    {
        try
        {
            var dlg = new StaffingAnalyticsWindow(CurrentSession?.BranchId);
            dlg.Owner = Application.Current.MainWindow;
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở phân tích nhân sự: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenAddEmployeeDialogAsync()
    {
        try
        {
            var roles = await EmployeeApiClient.Instance.GetRolesAsync();
            var branches = (await AuthenticationApiClient.Instance.GetBranchesAsync()).Where(b => b.IsActive).ToList();

            var dlg = new EmployeeEditWindow(roles, branches, null);
            if (dlg.ShowDialog() == true && dlg.ResultEmployee != null)
            {
                var req = new EmployeeCreateRequest
                {
                    EmployeeCode = dlg.ResultEmployee.EmployeeCode,
                    FullName = dlg.ResultEmployee.FullName,
                    Phone = dlg.ResultEmployee.Phone,
                    Email = dlg.ResultEmployee.Email,
                    CitizenId = dlg.ResultEmployee.CitizenId,
                    Status = dlg.ResultEmployee.Status,
                    Username = dlg.ResultUsername,
                    Password = "123456",
                    RoleId = dlg.ResultRoleId,
                    BranchId = dlg.ResultBranchId
                };

                var res = await EmployeeApiClient.Instance.CreateEmployeeAsync(req);
                if (res != null)
                {
                    await LoadEmployeeManagementDataAsync();
                    MessageBox.Show($"🎉 Đã thêm nhân viên '{res.FullName}' thành công!\n• Tài khoản: {dlg.ResultUsername}\n• Mật khẩu mặc định: 123456", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Thêm nhân viên thất bại hoặc tài khoản đã tồn tại!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thêm nhân viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task OpenEditEmployeeDialogAsync(EmployeeListItemViewModel? empVm)
    {
        if (empVm == null) return;
        try
        {
            var roles = await EmployeeApiClient.Instance.GetRolesAsync();
            var branches = (await AuthenticationApiClient.Instance.GetBranchesAsync()).Where(b => b.IsActive).ToList();

            var empEntity = new Employee
            {
                EmployeeId = empVm.EmployeeId,
                EmployeeCode = empVm.EmployeeCode,
                FullName = empVm.FullName,
                Phone = empVm.Phone,
                Email = empVm.Email,
                CitizenId = empVm.CitizenId,
                Status = empVm.Status,
                HireDate = empVm.HireDate
            };

            var dlg = new EmployeeEditWindow(roles, branches, empEntity, empVm.Username, empVm.RoleId, empVm.BranchId);
            if (dlg.ShowDialog() == true && dlg.ResultEmployee != null)
            {
                var req = new EmployeeUpdateRequest
                {
                    FullName = dlg.ResultEmployee.FullName,
                    Phone = dlg.ResultEmployee.Phone,
                    Email = dlg.ResultEmployee.Email,
                    CitizenId = dlg.ResultEmployee.CitizenId,
                    Status = dlg.ResultEmployee.Status,
                    Username = dlg.ResultUsername.Trim().ToLower(),
                    RoleId = dlg.ResultRoleId,
                    BranchId = dlg.ResultBranchId,
                    ResetPassword = dlg.ResetPasswordRequested
                };

                var res = await EmployeeApiClient.Instance.UpdateEmployeeAsync(empVm.EmployeeId, req);
                if (res != null)
                {
                    await LoadEmployeeManagementDataAsync();
                    MessageBox.Show($"🎉 Đã cập nhật hồ sơ nhân viên '{res.FullName}' thành công!" + (dlg.ResetPasswordRequested ? "\n• Đã đặt lại mật khẩu về: 123456" : ""), "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Cập nhật nhân viên thất bại!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            string errMsg = ex.InnerException != null ? $"{ex.Message}\nChi tiết: {ex.InnerException.Message}" : ex.Message;
            MessageBox.Show($"Lỗi cập nhật nhân viên: {errMsg}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ToggleEmployeeStatusAsync(EmployeeListItemViewModel? empVm)
    {
        if (empVm == null) return;
        try
        {
            var ok = await EmployeeApiClient.Instance.ToggleStatusAsync(empVm.EmployeeId);
            if (ok)
            {
                await LoadEmployeeManagementDataAsync();
            }
            else
            {
                MessageBox.Show("Không thể thay đổi trạng thái nhân viên!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi đổi trạng thái nhân viên: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ResetEmployeePasswordAsync(EmployeeListItemViewModel? empVm)
    {
        if (empVm == null || !empVm.UserId.HasValue) return;
        var res = MessageBox.Show($"Bạn có chắc chắn muốn đặt lại mật khẩu cho nhân viên '{empVm.FullName}' (User: {empVm.Username}) về '123456'?", "Xác nhận đặt lại mật khẩu", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                var ok = await EmployeeApiClient.Instance.ResetPasswordAsync(empVm.UserId.Value, "123456");
                if (ok)
                {
                    MessageBox.Show($"Đã đặt lại mật khẩu của '{empVm.Username}' về '123456' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Đặt lại mật khẩu thất bại!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đặt lại mật khẩu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
