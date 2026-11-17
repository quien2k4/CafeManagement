using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using CafeManagement.Wpf.Data;
using CafeManagement.Wpf.Models;
using CafeManagement.Wpf.Views;
using CafeManagement.Wpf.Services;
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
    private readonly CafeDbContext _db = new();
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
    private UserSession _currentSession = new();

    public bool CanAccessPos => CurrentSession.CanAccessPos;
    public bool CanAccessKds => CurrentSession.CanAccessKds;
    public bool CanAccessInventory => CurrentSession.CanAccessInventory;
    public bool CanAccessReport => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessProduct => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessCustomer => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanBrowseVouchers => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";
    public bool CanAccessEmployee => CurrentSession.IsOwnerOrAdmin || CurrentSession.RoleCode == "BRANCH_MANAGER";

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

    // ==================== LOGIC BÁN HÀNG POS & SEEDING ====================
    [RelayCommand]
    public async Task LoadDataAsync()
    {
        try
        {
            await EnsureSeedDataAsync();

            var cats = await _db.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            Categories = new ObservableCollection<ProductCategory>(cats);
            await FilterProductsAsync(null);

            // Kiểm tra ca làm việc đang mở của chi nhánh
            int curBranchId = CurrentSession.BranchId;
            CurrentShift = await _db.WorkShifts
                .Where(s => s.BranchId == curBranchId && s.Status == "OPEN")
                .OrderByDescending(s => s.ShiftId)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi kết nối CSDL: {ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EnsureSeedDataAsync()
    {
        // 1. Seed thêm món nếu thiếu
        var count = await _db.Products.CountAsync();
        if (count < 4)
        {
            var catCoffee = await _db.ProductCategories.FirstOrDefaultAsync(c => c.CategoryCode == "CAT_COFFEE");
            var catTeas = await _db.ProductCategories.FirstOrDefaultAsync(c => c.CategoryCode == "CAT_TEAS");
            var catCake = await _db.ProductCategories.FirstOrDefaultAsync(c => c.CategoryCode == "CAT_CAKE");

            if (catCoffee != null && !await _db.Products.AnyAsync(p => p.ProductCode == "CF_BAC_XIU"))
            {
                var bacXiu = new Product
                {
                    CategoryId = catCoffee.CategoryId,
                    ProductCode = "CF_BAC_XIU",
                    ProductName = "Bạc Xỉu Đá Sữa Tươi",
                    Description = "Nhiều sữa ít cà phê, béo ngậy thơm ngon",
                    BasePrice = 32000
                };
                bacXiu.Variants.Add(new ProductVariant { SkuCode = "CF_BAC_XIU_M", Size = "M", Temperature = "ICED", ExtraPrice = 0 });
                bacXiu.Variants.Add(new ProductVariant { SkuCode = "CF_BAC_XIU_L", Size = "L", Temperature = "ICED", ExtraPrice = 6000 });
                _db.Products.Add(bacXiu);
            }

            if (catTeas != null && !await _db.Products.AnyAsync(p => p.ProductCode == "TEA_DAO_CAM_SA"))
            {
                var traDao = new Product
                {
                    CategoryId = catTeas.CategoryId,
                    ProductCode = "TEA_DAO_CAM_SA",
                    ProductName = "Trà Đào Cam Sả",
                    Description = "Trà đen thanh mát kết hợp đào miếng giòn ngọt và hương sả",
                    BasePrice = 45000
                };
                traDao.Variants.Add(new ProductVariant { SkuCode = "TEA_DAO_CAM_SA_M", Size = "M", Temperature = "ICED", ExtraPrice = 0 });
                traDao.Variants.Add(new ProductVariant { SkuCode = "TEA_DAO_CAM_SA_L", Size = "L", Temperature = "ICED", ExtraPrice = 8000 });
                _db.Products.Add(traDao);
            }

            if (catTeas != null && !await _db.Products.AnyAsync(p => p.ProductCode == "TEA_SUA_OOLONG"))
            {
                var traSua = new Product
                {
                    CategoryId = catTeas.CategoryId,
                    ProductCode = "TEA_SUA_OOLONG",
                    ProductName = "Trà Sữa Oolong Nướng",
                    Description = "Trà oolong đậm vị rang nướng hòa quyện sữa béo",
                    BasePrice = 38000
                };
                traSua.Variants.Add(new ProductVariant { SkuCode = "TEA_SUA_OOLONG_M", Size = "M", Temperature = "ICED", ExtraPrice = 0 });
                traSua.Variants.Add(new ProductVariant { SkuCode = "TEA_SUA_OOLONG_L", Size = "L", Temperature = "ICED", ExtraPrice = 7000 });
                _db.Products.Add(traSua);
            }

            if (catCake != null && !await _db.Products.AnyAsync(p => p.ProductCode == "CAKE_CROISSANT"))
            {
                var banh = new Product
                {
                    CategoryId = catCake.CategoryId,
                    ProductCode = "CAKE_CROISSANT",
                    ProductName = "Bánh Croissant Bơ Tỏi",
                    Description = "Bánh sừng bò nướng giòn thơm mùi bơ Pháp",
                    BasePrice = 28000
                };
                banh.Variants.Add(new ProductVariant { SkuCode = "CAKE_CROISSANT_STD", Size = "M", Temperature = "HOT", ExtraPrice = 0 });
                _db.Products.Add(banh);
            }

            await _db.SaveChangesAsync();
        }

        // 2. Seed Nguyên liệu và Tồn kho chi nhánh 1
        var ingCount = await _db.Ingredients.CountAsync();
        if (ingCount < 6)
        {
            var defaultIngredients = new List<(string Code, string Name, string Uom, decimal Alert, decimal InitialStock)>
            {
                ("ING_SUATUOI", "Sữa tươi thanh trùng", "ml", 2000, 8000),
                ("ING_TRA_DEN", "Cốt trà đen hương đào", "ml", 1000, 5000),
                ("ING_TRA_OOLONG", "Cốt trà Oolong nướng", "ml", 1000, 5000),
                ("ING_DAO_MIENG", "Đào ngâm đóng hộp", "g", 500, 2500),
                ("ING_TRANCHAU", "Trân châu trắng 3Q", "g", 500, 3000),
                ("ING_KEMCHEESE", "Kem Cheese Macchiato", "ml", 500, 1500)
            };

            foreach (var di in defaultIngredients)
            {
                if (!await _db.Ingredients.AnyAsync(i => i.IngredientCode == di.Code))
                {
                    _db.Ingredients.Add(new Ingredient
                    {
                        IngredientCode = di.Code,
                        IngredientName = di.Name,
                        BaseUom = di.Uom,
                        MinStockAlert = di.Alert
                    });
                }
            }
            await _db.SaveChangesAsync();
        }

        // Khởi tạo tồn kho BranchInventory cho Branch 1
        var branch = await _db.Branches.FirstOrDefaultAsync() ?? new Branch { BranchId = 1 };
        var allIngs = await _db.Ingredients.ToListAsync();
        foreach (var ing in allIngs)
        {
            var existingStock = await _db.BranchInventories.FirstOrDefaultAsync(bi => bi.BranchId == branch.BranchId && bi.IngredientId == ing.IngredientId);
            if (existingStock == null)
            {
                decimal stock = ing.IngredientCode switch
                {
                    "ING_ROBUSTA" => 4500,
                    "ING_SUADAC" => 3000,
                    "ING_DA_VIEN" => 15000,
                    "ING_SUATUOI" => 6000,
                    "ING_TRA_DEN" => 4000,
                    "ING_TRA_OOLONG" => 3500,
                    "ING_DAO_MIENG" => 2000,
                    "ING_TRANCHAU" => 2500,
                    "ING_KEMCHEESE" => 1200,
                    _ => 2000
                };

                _db.BranchInventories.Add(new BranchInventory
                {
                    BranchId = branch.BranchId,
                    IngredientId = ing.IngredientId,
                    QuantityOnHand = stock,
                    ReservedQuantity = 0,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }
        await _db.SaveChangesAsync();

        // 3. Đảm bảo cấu trúc CSDL cho Khách hàng & Khuyến mãi (S04)
        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers')
                BEGIN
                    CREATE TABLE dbo.Customers (
                        CustomerId INT IDENTITY(1,1) PRIMARY KEY,
                        PhoneNumber VARCHAR(15) NOT NULL UNIQUE,
                        FullName NVARCHAR(100) NOT NULL,
                        MembershipTier VARCHAR(20) NOT NULL DEFAULT 'BRONZE',
                        TotalPoints INT NOT NULL DEFAULT 0,
                        LifetimePoints INT NOT NULL DEFAULT 0,
                        TotalSpent DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CreatedAt DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        IsActive BIT NOT NULL DEFAULT 1
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Promotions')
                BEGIN
                    CREATE TABLE dbo.Promotions (
                        PromotionId INT IDENTITY(1,1) PRIMARY KEY,
                        PromotionCode VARCHAR(30) NOT NULL UNIQUE,
                        PromotionName NVARCHAR(150) NOT NULL,
                        DiscountType VARCHAR(20) NOT NULL DEFAULT 'PERCENT',
                        DiscountValue DECIMAL(18,2) NOT NULL DEFAULT 0,
                        MaxDiscountAmount DECIMAL(18,2) NULL,
                        MinOrderAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        StartDate DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        EndDate DATETIME NOT NULL DEFAULT DATEADD(month, 3, GETUTCDATE()),
                        UsageLimit INT NULL,
                        UsedCount INT NOT NULL DEFAULT 0,
                        IsActive BIT NOT NULL DEFAULT 1
                    );
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'CustomerId')
                BEGIN
                    ALTER TABLE dbo.Orders ADD CustomerId INT NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'PromotionCode')
                BEGIN
                    ALTER TABLE dbo.Orders ADD PromotionCode VARCHAR(30) NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'PointsEarned')
                BEGIN
                    ALTER TABLE dbo.Orders ADD PointsEarned INT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'PointsUsed')
                BEGIN
                    ALTER TABLE dbo.Orders ADD PointsUsed INT NOT NULL DEFAULT 0;
                END

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Orders') AND name = 'ShiftId')
                BEGIN
                    ALTER TABLE dbo.Orders ADD ShiftId BIGINT NULL;
                END

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkShifts')
                BEGIN
                    CREATE TABLE dbo.WorkShifts (
                        ShiftId BIGINT IDENTITY(1,1) PRIMARY KEY,
                        ShiftCode VARCHAR(30) NOT NULL UNIQUE,
                        BranchId INT NOT NULL,
                        CashierUserId INT NOT NULL,
                        CashierName NVARCHAR(100) NOT NULL,
                        ShiftName NVARCHAR(50) NOT NULL DEFAULT N'Ca Sáng',
                        StartTime DATETIME NOT NULL DEFAULT GETUTCDATE(),
                        EndTime DATETIME NULL,
                        OpeningCash DECIMAL(18,2) NOT NULL DEFAULT 0,
                        CashSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        VietQrSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        TotalSales DECIMAL(18,2) NOT NULL DEFAULT 0,
                        TotalOrders INT NOT NULL DEFAULT 0,
                        ExpectedClosingCash DECIMAL(18,2) NOT NULL DEFAULT 0,
                        ActualClosingCash DECIMAL(18,2) NULL,
                        DifferenceAmount DECIMAL(18,2) NULL,
                        Status VARCHAR(20) NOT NULL DEFAULT 'OPEN',
                        Notes NVARCHAR(500) NULL
                    );
                END
            ");

            // Seed dữ liệu mẫu khách hàng nếu chưa có
            if (!await _db.Customers.AnyAsync())
            {
                _db.Customers.AddRange(
                    new Customer
                    {
                        PhoneNumber = "0901234567",
                        FullName = "Nguyễn Văn An",
                        MembershipTier = "BRONZE",
                        TotalPoints = 45,
                        LifetimePoints = 45,
                        TotalSpent = 450000,
                        CreatedAt = DateTime.UtcNow.AddMonths(-1)
                    },
                    new Customer
                    {
                        PhoneNumber = "0912345678",
                        FullName = "Trần Thị Bích",
                        MembershipTier = "SILVER",
                        TotalPoints = 140,
                        LifetimePoints = 140,
                        TotalSpent = 1400000,
                        CreatedAt = DateTime.UtcNow.AddMonths(-2)
                    },
                    new Customer
                    {
                        PhoneNumber = "0987654321",
                        FullName = "Lê Hoàng Long",
                        MembershipTier = "GOLD",
                        TotalPoints = 380,
                        LifetimePoints = 380,
                        TotalSpent = 3800000,
                        CreatedAt = DateTime.UtcNow.AddMonths(-3)
                    },
                    new Customer
                    {
                        PhoneNumber = "0977889900",
                        FullName = "Phạm Minh Trí",
                        MembershipTier = "DIAMOND",
                        TotalPoints = 720,
                        LifetimePoints = 720,
                        TotalSpent = 7200000,
                        CreatedAt = DateTime.UtcNow.AddMonths(-4)
                    }
                );
                await _db.SaveChangesAsync();
            }

            // Seed dữ liệu mẫu Voucher / Khuyến mãi nếu chưa có
            if (!await _db.Promotions.AnyAsync())
            {
                _db.Promotions.AddRange(
                    new Promotion
                    {
                        PromotionCode = "CHAOBANMOI",
                        PromotionName = "Chào Bạn Mới - Giảm 20%",
                        DiscountType = "PERCENT",
                        DiscountValue = 20,
                        MaxDiscountAmount = 30000,
                        MinOrderAmount = 0,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow.AddMonths(2),
                        UsageLimit = 500,
                        UsedCount = 12
                    },
                    new Promotion
                    {
                        PromotionCode = "GIAM15K",
                        PromotionName = "Ưu Đãi Giảm 15.000đ Cho Đơn Từ 50K",
                        DiscountType = "FIXED_AMOUNT",
                        DiscountValue = 15000,
                        MinOrderAmount = 50000,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow.AddMonths(2),
                        UsageLimit = 300,
                        UsedCount = 8
                    },
                    new Promotion
                    {
                        PromotionCode = "BUY1GET1",
                        PromotionName = "Mua 1 Tặng 1 (Món Rẻ Nhất)",
                        DiscountType = "BUY1_GET1",
                        DiscountValue = 100,
                        MinOrderAmount = 30000,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow.AddMonths(2),
                        UsageLimit = 200,
                        UsedCount = 5
                    },
                    new Promotion
                    {
                        PromotionCode = "VIPCOFFEE",
                        PromotionName = "Tri Ân Khách VIP - Giảm 25%",
                        DiscountType = "PERCENT",
                        DiscountValue = 25,
                        MaxDiscountAmount = 50000,
                        MinOrderAmount = 60000,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow.AddMonths(3),
                        UsageLimit = 100,
                        UsedCount = 2
                    }
                );
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi seed Customer/Promotion: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task FilterProductsAsync(ProductCategory? category)
    {
        SelectedCategory = category;
        IQueryable<Product> query = _db.Products
            .Include(p => p.Variants)
            .Where(p => p.IsActive);

        if (category != null && category.CategoryId > 0)
        {
            query = query.Where(p => p.CategoryId == category.CategoryId);
        }

        var list = await query.ToListAsync();
        Products = new ObservableCollection<Product>(list);
    }

    [RelayCommand]
    public async Task OpenCustomizeDialogAsync(Product product)
    {
        try
        {
            var toppings = await _db.Toppings.Where(t => t.IsActive).ToListAsync();
            var vm = new CustomizeProductViewModel(product, toppings);

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
            var toppings = await _db.Toppings.Where(t => t.IsActive).ToListAsync();
            var vm = new CustomizeProductViewModel(item.Product, toppings, item);

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

                var order = new Order
                {
                    OrderNumber = OrderNumber,
                    BranchId = branchId,
                    CashierUserId = cashierId,
                    CustomerId = SelectedCustomer?.CustomerId,
                    PromotionCode = SelectedPromotion?.PromotionCode,
                    PointsUsed = PointsToUse,
                    OrderType = "TAKE_AWAY",
                    OrderStatus = "PAID",
                    SubTotal = SubTotal,
                    DiscountAmount = DiscountAmount,
                    TaxAmount = 0,
                    FinalAmount = FinalAmount,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var cart in CartItems)
                {
                    var orderItem = new OrderItem
                    {
                        VariantId = cart.Variant.VariantId > 0 ? cart.Variant.VariantId : 1,
                        Quantity = cart.Quantity,
                        UnitPrice = cart.UnitPrice,
                        SubTotal = cart.LineTotal,
                        SugarLevel = cart.SugarLevel,
                        IceLevel = cart.IceLevel,
                        Note = cart.Note,
                        ItemStatus = "QUEUED"
                    };

                    foreach (var top in cart.Toppings)
                    {
                        orderItem.Toppings.Add(new OrderItemTopping
                        {
                            ToppingId = top.Topping.ToppingId,
                            Quantity = top.Quantity,
                            UnitPrice = top.Topping.Price
                        });
                    }

                    order.Items.Add(orderItem);
                }

                // Gán ca làm việc và cập nhật doanh thu ca
                if (CurrentShift != null)
                {
                    order.ShiftId = CurrentShift.ShiftId;
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

                    var dbShift = await _db.WorkShifts.FindAsync(CurrentShift.ShiftId);
                    if (dbShift != null)
                    {
                        dbShift.CashSales = CurrentShift.CashSales;
                        dbShift.VietQrSales = CurrentShift.VietQrSales;
                        dbShift.TotalSales = CurrentShift.TotalSales;
                        dbShift.TotalOrders = CurrentShift.TotalOrders;
                        dbShift.ExpectedClosingCash = CurrentShift.ExpectedClosingCash;
                    }
                }

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                // Lưu giao dịch thanh toán
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethod = checkoutVm.PaymentMethod,
                    Amount = FinalAmount,
                    Status = "SUCCESS",
                    TransactionCode = checkoutVm.PaymentMethod == "VIETQR" ? $"QR_{order.OrderNumber}" : null,
                    CashierUserId = cashierId,
                    PaidAt = DateTime.UtcNow
                };
                _db.Payments.Add(payment);

                // Lưu biên nhận thanh toán (Order Receipt)
                var receipt = new OrderReceipt
                {
                    ReceiptNumber = $"REC_{order.OrderNumber}",
                    OrderId = order.OrderId,
                    BranchId = branchId,
                    CashierUserId = cashierId,
                    TotalAmount = FinalAmount,
                    AmountPaid = checkoutVm.CustomerGivenAmount,
                    ChangeAmount = checkoutVm.ChangeAmount,
                    IssuedAt = DateTime.UtcNow
                };
                _db.OrderReceipts.Add(receipt);

                // Cập nhật điểm & nâng hạng khách hàng thành viên
                string customerInfoStr = "";
                if (SelectedCustomer != null)
                {
                    var dbCust = await _db.Customers.FindAsync(SelectedCustomer.CustomerId);
                    if (dbCust != null)
                    {
                        if (UseCustomerPoints && PointsToUse > 0)
                        {
                            dbCust.TotalPoints = Math.Max(0, dbCust.TotalPoints - PointsToUse);
                        }

                        int earned = (int)(FinalAmount / 10000);
                        dbCust.TotalPoints += earned;
                        dbCust.LifetimePoints += earned;
                        dbCust.TotalSpent += FinalAmount;
                        order.PointsEarned = earned;

                        // Tự động nâng hạng thành viên
                        string oldTier = dbCust.MembershipTier;
                        if (dbCust.LifetimePoints >= 600) dbCust.MembershipTier = "DIAMOND";
                        else if (dbCust.LifetimePoints >= 300) dbCust.MembershipTier = "GOLD";
                        else if (dbCust.LifetimePoints >= 100) dbCust.MembershipTier = "SILVER";

                        customerInfoStr = $"\n\n👤 KHÁCH HÀNG THÀNH VIÊN:\n" +
                                          $"• Khách hàng: {dbCust.FullName} ({dbCust.PhoneNumber})\n" +
                                          $"• Hạng: {dbCust.TierBadge}" +
                                          (oldTier != dbCust.MembershipTier ? " 🌟 (Chúc mừng thăng hạng!)" : "") +
                                          (PointsToUse > 0 ? $"\n• Điểm đã đổi: -{PointsToUse} điểm (-{PointsDiscountAmount:N0} đ)" : "") +
                                          $"\n• Điểm tích lũy mới: +{earned} điểm (Số dư: {dbCust.TotalPoints} điểm)";
                    }
                }

                // Cập nhật lượt sử dụng Voucher
                string promoInfoStr = "";
                if (SelectedPromotion != null)
                {
                    var dbPromo = await _db.Promotions.FindAsync(SelectedPromotion.PromotionId);
                    if (dbPromo != null)
                    {
                        dbPromo.UsedCount++;
                    }
                    promoInfoStr = $"\n\n🎟️ ƯU ĐÃI: {SelectedPromotion.PromotionName} (-{VoucherDiscountAmount:N0} đ)";
                }

                // ==================== TỰ ĐỘNG TRỪ KHO NGUYÊN LIỆU THEO CÔNG THỨC ====================
                await DeductInventoryForOrderAsync(order, branchId);

                await _db.SaveChangesAsync();

                string methodStr = checkoutVm.PaymentMethod == "CASH" ? "Tiền mặt" : "Chuyển khoản VietQR";
                string changeStr = checkoutVm.PaymentMethod == "CASH" ? $"\nTiền thối lại: {checkoutVm.ChangeAmount:N0} đ" : "";

                MessageBox.Show(
                    $"🎉 THANH TOÁN THÀNH CÔNG!\n\n" +
                    $"• Mã đơn: #{order.OrderNumber}\n" +
                    $"• Tổng thanh toán: {FinalAmount:N0} đ\n" +
                    $"• Hình thức: {methodStr}{changeStr}\n" +
                    $"• Biên nhận: {receipt.ReceiptNumber}" +
                    promoInfoStr + customerInfoStr + "\n\n" +
                    $"⚡ Đã tự động trừ nguyên liệu trong kho và đẩy sang Quầy Barista!",
                    "Hoàn tất đơn hàng", MessageBoxButton.OK, MessageBoxImage.Information);

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
        try
        {
            string phone = CustomerSearchPhone.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                MessageBox.Show("Vui lòng nhập số điện thoại khách hàng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var cust = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
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
                        _db.Customers.Add(dlg.CreatedCustomer);
                        await _db.SaveChangesAsync();
                        SelectedCustomer = dlg.CreatedCustomer;
                        UseCustomerPoints = false;
                        CustomerSearchPhone = "";
                        UpdateTotals();
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
        try
        {
            string code = VoucherInputCode.Trim().ToUpper();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("Vui lòng nhập mã Voucher / Khuyến mãi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.PromotionCode == code);
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

            var now = DateTime.UtcNow;
            var activePromos = await _db.Promotions
                .Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now && (!p.UsageLimit.HasValue || p.UsedCount < p.UsageLimit.Value))
                .OrderBy(p => p.MinOrderAmount)
                .ToListAsync();

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

    // HÀM TRỪ KHO THEO ĐỊNH LƯỢNG MÓN VÀ TOPPING
    private async Task DeductInventoryForOrderAsync(Order order, int branchId)
    {
        try
        {
            foreach (var item in order.Items)
            {
                var variant = await _db.ProductVariants.Include(v => v.Product).FirstOrDefaultAsync(v => v.VariantId == item.VariantId);
                if (variant == null) continue;

                // Xác định lượng tiêu hao nguyên liệu dựa theo món và size
                var deductions = new List<(string IngCode, decimal Amount)>();

                // 1. Ưu tiên kiểm tra công thức BOM đã cấu hình trong CSDL (Recipes -> RecipeVersions -> RecipeDetails)
                var recipeVersion = await _db.RecipeVersions
                    .Include(rv => rv.Details)
                        .ThenInclude(d => d.Ingredient)
                    .Where(rv => rv.Recipe!.VariantId == variant.VariantId && rv.IsCurrent)
                    .FirstOrDefaultAsync();

                if (recipeVersion != null && recipeVersion.Details.Count > 0)
                {
                    foreach (var detail in recipeVersion.Details)
                    {
                        if (detail.Ingredient != null)
                        {
                            deductions.Add((detail.Ingredient.IngredientCode, detail.Quantity));
                        }
                    }
                }
                else
                {
                    // Fallback theo định lượng mặc định
                    string pCode = variant.Product?.ProductCode ?? "";
                    bool isSizeL = variant.Size == "L";

                    if (pCode.Contains("CF_SUA_DA"))
                    {
                        deductions.Add(("ING_ROBUSTA", isSizeL ? 35 : 25));
                        deductions.Add(("ING_SUADAC", isSizeL ? 50 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("CF_BAC_XIU"))
                    {
                        deductions.Add(("ING_ROBUSTA", isSizeL ? 20 : 15));
                        deductions.Add(("ING_SUADAC", isSizeL ? 50 : 40));
                        deductions.Add(("ING_SUATUOI", isSizeL ? 130 : 100));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TEA_DAO_CAM_SA"))
                    {
                        deductions.Add(("ING_TRA_DEN", isSizeL ? 200 : 150));
                        deductions.Add(("ING_DAO_MIENG", isSizeL ? 60 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                    else if (pCode.Contains("TEA_SUA_OOLONG"))
                    {
                        deductions.Add(("ING_TRA_OOLONG", isSizeL ? 200 : 150));
                        deductions.Add(("ING_SUADAC", isSizeL ? 50 : 40));
                        deductions.Add(("ING_DA_VIEN", isSizeL ? 200 : 150));
                    }
                }

                // Tiêu hao từ Topping đi kèm
                foreach (var top in item.Toppings)
                {
                    var topping = await _db.Toppings.FindAsync(top.ToppingId);
                    if (topping != null)
                    {
                        if (topping.ToppingCode.Contains("TRANCHAU"))
                            deductions.Add(("ING_TRANCHAU", 30 * top.Quantity));
                        else if (topping.ToppingCode.Contains("KEMCHEESE"))
                            deductions.Add(("ING_KEMCHEESE", 40 * top.Quantity));
                        else if (topping.ToppingCode.Contains("SUA"))
                            deductions.Add(("ING_SUADAC", 20 * top.Quantity));
                    }
                }

                // Trừ vào bảng BranchInventory và lưu Thẻ kho
                foreach (var d in deductions)
                {
                    decimal totalDeduct = d.Amount * item.Quantity;
                    var ing = await _db.Ingredients.FirstOrDefaultAsync(i => i.IngredientCode == d.IngCode);
                    if (ing == null) continue;

                    var stock = await _db.BranchInventories.FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == ing.IngredientId);
                    if (stock != null)
                    {
                        stock.QuantityOnHand = Math.Max(0, stock.QuantityOnHand - totalDeduct);
                        stock.UpdatedAt = DateTime.UtcNow;

                        _db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            BranchId = branchId,
                            IngredientId = ing.IngredientId,
                            TransactionType = "SALE_CONSUMPTION",
                            QuantityChange = -totalDeduct,
                            BalanceAfter = stock.QuantityOnHand,
                            ReferenceType = "ORDER",
                            ReferenceId = order.OrderNumber,
                            Note = $"Xuất bán {item.Quantity}x {variant.Product?.ProductName} ({variant.Size})",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lỗi trừ kho: {ex.Message}");
        }
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

            var orders = await _db.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v!.Product)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Toppings)
                        .ThenInclude(t => t.Topping)
                .Where(o => o.BranchId == branchId && (o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY"))
                .OrderBy(o => o.CreatedAt)
                .ToListAsync();

            var tickets = new List<KdsTicketViewModel>();
            foreach (var ord in orders)
            {
                var ticket = new KdsTicketViewModel
                {
                    Order = ord,
                    OrderStatus = ord.OrderStatus
                };

                foreach (var itm in ord.Items)
                {
                    var topSummary = itm.Toppings.Count > 0
                        ? "+ " + string.Join(", ", itm.Toppings.Select(t => t.Quantity > 1 ? $"{t.Topping?.ToppingName} (x{t.Quantity})" : t.Topping?.ToppingName))
                        : "";

                    var optParts = new List<string> { $"Size {itm.Variant?.Size ?? "M"}", $"{itm.SugarLevel} đường", $"{itm.IceLevel} đá" };
                    if (!string.IsNullOrEmpty(topSummary)) optParts.Add(topSummary);

                    ticket.Items.Add(new KdsItemViewModel
                    {
                        OrderItemId = itm.OrderItemId,
                        OrderId = ord.OrderId,
                        ProductName = itm.Variant?.Product?.ProductName ?? "Món nước",
                        Size = itm.Variant?.Size ?? "M",
                        Quantity = itm.Quantity,
                        OptionsSummary = string.Join(" | ", optParts),
                        Note = itm.Note ?? "",
                        Status = itm.ItemStatus
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

            var dbItem = await _db.OrderItems.FindAsync(item.OrderItemId);
            if (dbItem != null)
            {
                dbItem.ItemStatus = "PREPARING";
            }

            var dbOrder = await _db.Orders.FindAsync(item.OrderId);
            if (dbOrder != null && dbOrder.OrderStatus == "PAID")
            {
                dbOrder.OrderStatus = "PREPARING";
            }

            await _db.SaveChangesAsync();
            await RefreshKdsAsync();
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

            var dbItem = await _db.OrderItems.FindAsync(item.OrderItemId);
            if (dbItem != null)
            {
                dbItem.ItemStatus = "READY";
            }

            var allItemsInOrder = await _db.OrderItems.Where(i => i.OrderId == item.OrderId).ToListAsync();
            bool allReady = allItemsInOrder.All(i => i.OrderItemId == item.OrderItemId || i.ItemStatus == "READY" || i.ItemStatus == "SERVED");

            var dbOrder = await _db.Orders.FindAsync(item.OrderId);
            if (dbOrder != null && allReady)
            {
                dbOrder.OrderStatus = "READY";
            }

            await _db.SaveChangesAsync();
            await RefreshKdsAsync();
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
            var dbOrder = await _db.Orders.FindAsync(ticket.OrderId);
            if (dbOrder != null)
            {
                dbOrder.OrderStatus = "COMPLETED";
            }

            var dbItems = await _db.OrderItems.Where(i => i.OrderId == ticket.OrderId).ToListAsync();
            foreach (var itm in dbItems)
            {
                itm.ItemStatus = "SERVED";
            }

            await _db.SaveChangesAsync();

            MessageBox.Show($"🎉 Đã bàn giao đơn hàng #{ticket.OrderNumber} cho khách thành công!", 
                "Bàn Giao Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);

            await RefreshKdsAsync();
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
        try
        {
            int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;

            // Tải danh sách tồn kho chi nhánh kèm thông tin nguyên liệu
            var stocks = await _db.BranchInventories
                .Include(bi => bi.Ingredient)
                .Where(bi => bi.BranchId == branchId)
                .OrderBy(bi => bi.Ingredient!.IngredientName)
                .ToListAsync();

            var items = stocks.Select(s => new InventoryItemViewModel
            {
                BranchInventoryId = s.BranchInventoryId,
                IngredientId = s.IngredientId,
                IngredientCode = s.Ingredient?.IngredientCode ?? "",
                IngredientName = s.Ingredient?.IngredientName ?? "Nguyên liệu",
                BaseUom = s.Ingredient?.BaseUom ?? "g",
                QuantityOnHand = s.QuantityOnHand,
                MinStockAlert = s.Ingredient?.MinStockAlert ?? 0
            }).ToList();

            InventoryItems = new ObservableCollection<InventoryItemViewModel>(items);
            LowStockCount = items.Count(i => i.IsLowStock);

            // Tải đến 500 giao dịch Thẻ kho gần nhất
            var trans = await _db.InventoryTransactions
                .Include(t => t.Ingredient)
                .Where(t => t.BranchId == branchId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(500)
                .ToListAsync();

            _allInventoryTransactions = trans.Select(t => new InventoryTransactionItemViewModel
            {
                TransactionId = t.TransactionId,
                BranchId = t.BranchId,
                IngredientId = t.IngredientId,
                TransactionType = t.TransactionType,
                CreatedAt = t.CreatedAt.ToLocalTime(),
                IngredientName = t.Ingredient?.IngredientName ?? "",
                QuantityChange = t.QuantityChange,
                BalanceAfter = t.BalanceAfter,
                BaseUom = t.Ingredient?.BaseUom ?? "g",
                ReferenceId = t.ReferenceId,
                Note = t.Note ?? ""
            }).ToList();

            ApplyInventoryTransFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu kho: {ex.Message}", "Lỗi Kho", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var stock = await _db.BranchInventories
                .FirstOrDefaultAsync(bi => bi.BranchId == item.BranchId && bi.IngredientId == item.IngredientId);

            if (stock != null)
            {
                stock.QuantityOnHand -= item.QuantityChange;
                if (stock.QuantityOnHand < 0) stock.QuantityOnHand = 0;
                stock.UpdatedAt = DateTime.UtcNow;
            }

            // Ghi nhận Thẻ kho cho giao dịch HỦY PHIẾU NHẬP
            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                BranchId = item.BranchId,
                IngredientId = item.IngredientId,
                TransactionType = "RECEIPT_CANCELLED",
                QuantityChange = -item.QuantityChange,
                BalanceAfter = stock?.QuantityOnHand ?? 0,
                ReferenceType = "CANCEL_RECEIPT",
                ReferenceId = $"HUY_{item.ReferenceId}",
                Note = $"[HỦY PHIẾU #{item.ReferenceId}] Thu hồi do nhập sai: -{item.QuantityChange:N1} {item.BaseUom}",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            MessageBox.Show($"✅ Đã hủy phiếu #{item.ReferenceId} và thu hồi -{item.QuantityChange:N1} {item.BaseUom} {item.IngredientName} thành công!",
                "Đã Hủy Phiếu Nhập", MessageBoxButton.OK, MessageBoxImage.Information);

            await RefreshInventoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi hủy phiếu nhập kho: {ex.Message}", "Lỗi Hủy Phiếu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Mở hộp thoại Nhập kho nhanh
    [RelayCommand]
    public async Task OpenImportStockDialogAsync()
    {
        try
        {
            var allIngredients = await _db.Ingredients.Where(i => i.IsActive).OrderBy(i => i.IngredientName).ToListAsync();
            var dialog = new ImportStockWindow(allIngredients);
            if (Application.Current?.MainWindow != null)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            if (dialog.ShowDialog() == true && dialog.SelectedIngredient != null)
            {
                int branchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;
                var stock = await _db.BranchInventories
                    .FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == dialog.SelectedIngredient.IngredientId);

                if (stock == null)
                {
                    stock = new BranchInventory
                    {
                        BranchId = branchId,
                        IngredientId = dialog.SelectedIngredient.IngredientId,
                        QuantityOnHand = dialog.ImportQuantity,
                        ReservedQuantity = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.BranchInventories.Add(stock);
                }
                else
                {
                    stock.QuantityOnHand += dialog.ImportQuantity;
                    stock.UpdatedAt = DateTime.UtcNow;
                }

                // Ghi nhận Thẻ kho (GOODS_RECEIPT)
                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BranchId = branchId,
                    IngredientId = dialog.SelectedIngredient.IngredientId,
                    TransactionType = "GOODS_RECEIPT",
                    QuantityChange = dialog.ImportQuantity,
                    BalanceAfter = stock.QuantityOnHand,
                    ReferenceType = "GOODS_RECEIPT",
                    ReferenceId = $"PN{DateTime.Now:yyMMddHHmm}",
                    Note = dialog.Note,
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                MessageBox.Show($"🎉 Đã nhập kho thành công +{dialog.ImportQuantity:N0} {dialog.SelectedIngredient.BaseUom} {dialog.SelectedIngredient.IngredientName}!", 
                    "Nhập Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshInventoryAsync();
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
                var newIng = dlg.IngredientToSave;
                newIng.MinStockAlert = dlg.MinStockAlert;
                _db.Ingredients.Add(newIng);
                await _db.SaveChangesAsync();

                // Tạo BranchInventory cho các chi nhánh
                var branches = await _db.Branches.ToListAsync();
                int currentBranchId = CurrentSession.BranchId > 0 ? CurrentSession.BranchId : 1;

                foreach (var b in branches)
                {
                    decimal initQty = (b.BranchId == currentBranchId) ? dlg.InitialStock : 0;
                    var branchStock = new BranchInventory
                    {
                        BranchId = b.BranchId,
                        IngredientId = newIng.IngredientId,
                        QuantityOnHand = initQty,
                        ReservedQuantity = 0,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.BranchInventories.Add(branchStock);

                    if (initQty > 0)
                    {
                        _db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            BranchId = b.BranchId,
                            IngredientId = newIng.IngredientId,
                            TransactionType = "INITIAL_STOCK",
                            QuantityChange = initQty,
                            BalanceAfter = initQty,
                            ReferenceType = "INITIAL",
                            ReferenceId = $"INIT_{newIng.IngredientCode}",
                            Note = $"Khởi tạo tồn kho ban đầu khi tạo nguyên liệu mới ({CurrentCashier})",
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _db.SaveChangesAsync();

                MessageBox.Show($"🎉 Đã thêm thành công nguyên liệu '{newIng.IngredientName}' vào hệ thống kho!",
                    "Thêm Nguyên Liệu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshInventoryAsync();
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
            var ing = await _db.Ingredients.FindAsync(item.IngredientId);
            if (ing == null) return;

            var dlg = new IngredientEditWindow(ing, item.MinStockAlert);
            if (Application.Current?.MainWindow != null)
            {
                dlg.Owner = Application.Current.MainWindow;
            }

            if (dlg.ShowDialog() == true)
            {
                ing.MinStockAlert = dlg.MinStockAlert;
                await _db.SaveChangesAsync();

                MessageBox.Show($"🎉 Đã cập nhật thành công nguyên liệu '{ing.IngredientName}'!",
                    "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshInventoryAsync();
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
                var stock = await _db.BranchInventories
                    .FirstOrDefaultAsync(bi => bi.BranchId == branchId && bi.IngredientId == item.IngredientId);

                if (stock == null) return;

                decimal oldQty = stock.QuantityOnHand;
                decimal newQty = dlg.NewQuantity;
                decimal diff = newQty - oldQty;

                stock.QuantityOnHand = newQty;
                stock.UpdatedAt = DateTime.UtcNow;

                // Lưu vết lịch sử thẻ kho (Audit Log)
                string auditRef = $"KK{DateTime.Now:yyMMddHHmm}";
                string noteDetail = string.IsNullOrWhiteSpace(dlg.Note) ? "" : $" ({dlg.Note})";

                _db.InventoryTransactions.Add(new InventoryTransaction
                {
                    BranchId = branchId,
                    IngredientId = item.IngredientId,
                    TransactionType = "STOCK_ADJUSTMENT",
                    QuantityChange = diff,
                    BalanceAfter = newQty,
                    ReferenceType = "ADJUSTMENT",
                    ReferenceId = auditRef,
                    Note = $"[Kiểm kê] {dlg.Reason}{noteDetail} | Cũ: {oldQty:N1}{item.BaseUom} -> Mới: {newQty:N1}{item.BaseUom} | Bởi: {CurrentCashier}",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                MessageBox.Show($"🎉 Đã điều chỉnh kiểm kê tồn kho cho '{item.IngredientName}' thành công!\n\n" +
                                $"• Số lượng cũ: {oldQty:N1} {item.BaseUom}\n" +
                                $"• Số lượng mới: {newQty:N1} {item.BaseUom}\n" +
                                $"• Chênh lệch: {(diff >= 0 ? "+" : "")}{diff:N1} {item.BaseUom}\n" +
                                $"• Mã phiếu kiểm kê: #{auditRef}\n\n" +
                                $"⚡ Lịch sử đã được ghi nhận vào thẻ kho.",
                                "Điều Chỉnh Tồn Kho Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

                await RefreshInventoryAsync();
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
        ReportDateFilterText = filter switch
        {
            "TODAY" => "Hôm nay",
            "WEEK" => "7 ngày qua",
            "MONTH" => "Tháng này",
            _ => "Tất cả thời gian"
        };
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
            var query = _db.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Variant)
                        .ThenInclude(v => v!.Product)
                .Include(o => o.Payments)
                .Include(o => o.Branch)
                .Where(o => o.OrderStatus == "PAID" || o.OrderStatus == "PREPARING" || o.OrderStatus == "READY" || o.OrderStatus == "SERVED");

            // 1. Lọc theo Chi nhánh
            if (ReportBranchFilter == "CURRENT" && CurrentSession.BranchId > 0)
            {
                query = query.Where(o => o.BranchId == CurrentSession.BranchId);
            }

            // 2. Lọc theo Ngày
            var now = DateTime.UtcNow;
            if (ReportDateFilter == "TODAY")
            {
                var todayStart = DateTime.UtcNow.Date;
                query = query.Where(o => o.CreatedAt >= todayStart);
            }
            else if (ReportDateFilter == "WEEK")
            {
                var weekStart = DateTime.UtcNow.Date.AddDays(-7);
                query = query.Where(o => o.CreatedAt >= weekStart);
            }
            else if (ReportDateFilter == "MONTH")
            {
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                query = query.Where(o => o.CreatedAt >= monthStart);
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            // 3. Tính toán các chỉ số KPI
            ReportTotalOrders = orders.Count;
            ReportTotalRevenue = orders.Sum(o => o.FinalAmount);
            ReportTotalCups = orders.Sum(o => o.Items.Sum(i => i.Quantity));
            ReportAverageOrderValue = ReportTotalOrders > 0 ? Math.Round(ReportTotalRevenue / ReportTotalOrders, 0) : 0;

            // 4. Tỷ trọng Tiền mặt vs VietQR
            var allPayments = orders.SelectMany(o => o.Payments).ToList();
            var cashPayments = allPayments.Where(p => p.PaymentMethod == "CASH").ToList();
            var qrPayments = allPayments.Where(p => p.PaymentMethod == "VIETQR").ToList();

            ReportCashRevenue = cashPayments.Sum(p => p.Amount);
            ReportCashCount = cashPayments.Count;

            ReportQrRevenue = qrPayments.Sum(p => p.Amount);
            ReportQrCount = qrPayments.Count;

            decimal totalPaymentAmount = ReportCashRevenue + ReportQrRevenue;
            if (totalPaymentAmount > 0)
            {
                ReportCashPercent = Math.Round((double)(ReportCashRevenue / totalPaymentAmount) * 100.0, 1);
                ReportQrPercent = Math.Round((double)(ReportQrRevenue / totalPaymentAmount) * 100.0, 1);
            }
            else
            {
                ReportCashPercent = 50;
                ReportQrPercent = 50;
            }

            // 5. Xếp hạng Top 10 món bán chạy nhất (Best Sellers)
            var productSales = orders
                .SelectMany(o => o.Items)
                .GroupBy(i => i.Variant?.Product?.ProductName ?? "Món nước")
                .Select(g => new
                {
                    ProductName = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Revenue = g.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.Quantity)
                .Take(10)
                .ToList();

            int rank = 1;
            var topList = productSales.Select(p => new TopProductItemViewModel
            {
                Rank = rank++,
                ProductName = p.ProductName,
                TotalQuantity = p.Quantity,
                TotalRevenue = p.Revenue,
                PercentOfTotal = ReportTotalRevenue > 0 ? Math.Round((double)(p.Revenue / ReportTotalRevenue) * 100.0, 1) : 0
            }).ToList();

            ReportTopProducts = new ObservableCollection<TopProductItemViewModel>(topList);

            // 6. Lịch sử toàn bộ hóa đơn bán hàng theo bộ lọc
            var cashiers = await _db.UserAccounts.Include(u => u.Employee).ToDictionaryAsync(u => u.UserId, u => u.Employee?.FullName ?? u.Username);

            _allReportOrders = orders.Select(o =>
            {
                string cName = cashiers.TryGetValue(o.CashierUserId, out var cn) ? cn : $"Thu ngân #{o.CashierUserId}";
                string pMethod = o.Payments.FirstOrDefault()?.PaymentMethod ?? "CASH";
                string summary = string.Join(", ", o.Items.Select(i => $"{i.Quantity}x {i.Variant?.Product?.ProductName}"));

                return new OrderReportItemViewModel
                {
                    OrderNumber = o.OrderNumber,
                    CreatedAt = o.CreatedAt.ToLocalTime(),
                    CashierName = cName,
                    BranchName = o.Branch?.BranchName ?? "Chi nhánh",
                    PaymentMethod = pMethod,
                    ItemsSummary = summary,
                    FinalAmount = o.FinalAmount
                };
            }).ToList();

            ApplyOrderReportFilter();
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

    // ==================== 6. QUẢN LÝ MÓN NƯỚC, GIÁ & CÔNG THỨC PHA CHẾ (BOM) ====================
    private List<ProductManagementItemViewModel> _allManagedProducts = [];

    [RelayCommand]
    public async Task LoadManagementProductsAsync()
    {
        try
        {
            var cats = await _db.ProductCategories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            ManagementCategories = new ObservableCollection<ProductCategory>(cats);

            var products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .OrderByDescending(p => p.ProductId)
                .ToListAsync();

            _allManagedProducts = products.Select(p => new ProductManagementItemViewModel
            {
                ProductId = p.ProductId,
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                CategoryName = p.Category?.CategoryName ?? "Khác",
                BasePrice = p.BasePrice,
                Description = p.Description ?? "",
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
    public async Task OpenAddProductDialogAsync()
    {
        try
        {
            var categories = await _db.ProductCategories.OrderBy(c => c.DisplayOrder).ToListAsync();
            var dlg = new ProductEditWindow(categories);
            if (dlg.ShowDialog() == true && dlg.ProductToSave != null)
            {
                _db.Products.Add(dlg.ProductToSave);
                await _db.SaveChangesAsync();

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
            var categories = await _db.ProductCategories.OrderBy(c => c.DisplayOrder).ToListAsync();
            var product = await _db.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

            if (product == null) return;

            var dlg = new ProductEditWindow(categories, product);
            if (dlg.ShowDialog() == true)
            {
                await _db.SaveChangesAsync();

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
            var product = await _db.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

            if (product == null) return;

            var ingredients = await _db.Ingredients.OrderBy(i => i.IngredientName).ToListAsync();
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
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product != null)
            {
                product.IsActive = !product.IsActive;
                await _db.SaveChangesAsync();
                item.IsActive = product.IsActive;

                // Cập nhật lại menu bán hàng POS
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
            var custs = await _db.Customers.OrderByDescending(c => c.TotalSpent).ToListAsync();
            _allCustomers = custs;
            TotalCustomerCount = custs.Count;

            ApplyCustomerFilter();

            var promos = await _db.Promotions.OrderByDescending(p => p.StartDate).ToListAsync();
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
                if (await _db.Customers.AnyAsync(c => c.PhoneNumber == dlg.CreatedCustomer.PhoneNumber))
                {
                    MessageBox.Show("Số điện thoại này đã được đăng ký thành viên!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _db.Customers.Add(dlg.CreatedCustomer);
                await _db.SaveChangesAsync();
                await LoadCustomerManagementDataAsync();
                MessageBox.Show($"🎉 Đã đăng ký thành viên cho '{dlg.CreatedCustomer.FullName}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var dbCust = await _db.Customers.FindAsync(customer.CustomerId);
            if (dbCust == null) return;

            var dlg = new AdjustCustomerPointsWindow(dbCust);
            if (dlg.ShowDialog() == true)
            {
                dbCust.TotalPoints = Math.Max(0, dbCust.TotalPoints + dlg.PointDelta);
                if (dlg.PointDelta > 0)
                {
                    dbCust.LifetimePoints += dlg.PointDelta;
                }
                dbCust.MembershipTier = dlg.SelectedTier;

                await _db.SaveChangesAsync();
                await LoadCustomerManagementDataAsync();
                MessageBox.Show($"🎉 Đã điều chỉnh điểm & hạng thành viên cho '{dbCust.FullName}' thành công!\n• Điểm hiện tại: {dbCust.TotalPoints} điểm\n• Hạng: {dbCust.MembershipTier}", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (await _db.Promotions.AnyAsync(p => p.PromotionCode == dlg.PromoToSave.PromotionCode))
                {
                    MessageBox.Show("Mã khuyến mãi này đã tồn tại trong hệ thống!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _db.Promotions.Add(dlg.PromoToSave);
                await _db.SaveChangesAsync();
                await LoadCustomerManagementDataAsync();
                MessageBox.Show($"🎉 Đã tạo voucher '{dlg.PromoToSave.PromotionCode}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var dbPromo = await _db.Promotions.FindAsync(promo.PromotionId);
            if (dbPromo == null) return;

            var dlg = new PromotionEditWindow(dbPromo);
            if (dlg.ShowDialog() == true)
            {
                await _db.SaveChangesAsync();
                await LoadCustomerManagementDataAsync();
                MessageBox.Show($"🎉 Đã cập nhật voucher '{dbPromo.PromotionCode}' thành công!", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var dbPromo = await _db.Promotions.FindAsync(promo.PromotionId);
            if (dbPromo != null)
            {
                dbPromo.IsActive = !dbPromo.IsActive;
                await _db.SaveChangesAsync();
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
                _db.WorkShifts.Add(dlg.CreatedShift);
                await _db.SaveChangesAsync();
                CurrentShift = dlg.CreatedShift;

                MessageBox.Show($"🎉 Đã mở ca '{CurrentShift.ShiftName}' (#{CurrentShift.ShiftCode}) thành công!\n" +
                                $"• Tiền đầu ca: {CurrentShift.OpeningCash:N0} đ\n" +
                                $"• Thu ngân: {CurrentShift.CashierName}\n" +
                                $"Chúc bạn một ca làm việc thuận lợi!",
                                "Mở Ca Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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

            var dbShift = await _db.WorkShifts.FindAsync(CurrentShift.ShiftId);
            if (dbShift == null) return;

            var dlg = new ShiftCloseWindow(dbShift);
            if (dlg.ShowDialog() == true)
            {
                dbShift.EndTime = DateTime.UtcNow;
                dbShift.ActualClosingCash = dlg.ActualCash;
                dbShift.DifferenceAmount = dlg.Difference;
                dbShift.Notes = dlg.ClosingNotes;
                dbShift.Status = "CLOSED";

                await _db.SaveChangesAsync();

                CurrentShift = null;

                // Mở Phiếu kết ca Z-Report
                var rptDlg = new ZReportWindow(dbShift, CurrentSession.BranchName);
                rptDlg.ShowDialog();
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
            var emps = await _db.Employees.OrderBy(e => e.EmployeeId).ToListAsync();
            var users = await _db.UserAccounts.Include(u => u.Employee).ToListAsync();
            var assignments = await _db.UserRoleBranchAssignments
                .Include(a => a.Role)
                .Include(a => a.Branch)
                .ToListAsync();

            var list = new List<EmployeeListItemViewModel>();
            foreach (var emp in emps)
            {
                var user = users.FirstOrDefault(u => u.EmployeeId == emp.EmployeeId);
                var assign = user != null ? assignments.FirstOrDefault(a => a.UserId == user.UserId) : null;

                list.Add(new EmployeeListItemViewModel
                {
                    EmployeeId = emp.EmployeeId,
                    EmployeeCode = emp.EmployeeCode,
                    FullName = emp.FullName,
                    Phone = emp.Phone,
                    Email = emp.Email,
                    CitizenId = emp.CitizenId,
                    Status = emp.Status,
                    HireDate = emp.HireDate,
                    UserId = user?.UserId,
                    Username = user?.Username ?? "",
                    RoleId = assign?.RoleId,
                    RoleCode = assign?.Role?.RoleCode ?? "",
                    RoleName = assign?.Role?.RoleName ?? "Chưa phân vai trò",
                    BranchId = assign?.BranchId,
                    BranchName = assign?.Branch?.BranchName ?? "Chưa gán chi nhánh"
                });
            }

            _allEmployees = list;
            TotalEmployeeCount = list.Count;
            ApplyEmployeeFilter();

            var shifts = await _db.WorkShifts
                .Include(s => s.Branch)
                .OrderByDescending(s => s.ShiftId)
                .ToListAsync();

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
    public async Task OpenAddEmployeeDialogAsync()
    {
        try
        {
            var roles = await _db.Roles.ToListAsync();
            var branches = await _db.Branches.Where(b => b.IsActive).ToListAsync();

            var dlg = new EmployeeEditWindow(roles, branches, null);
            if (dlg.ShowDialog() == true && dlg.ResultEmployee != null)
            {
                if (await _db.UserAccounts.AnyAsync(u => u.Username.ToLower() == dlg.ResultUsername.ToLower()))
                {
                    MessageBox.Show($"Tên đăng nhập '{dlg.ResultUsername}' đã tồn tại!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _db.Employees.Add(dlg.ResultEmployee);
                await _db.SaveChangesAsync();

                string salt = "SALT_CAFE_2026";
                string pwdHash = AuthService.HashMd5("123456");

                var userAcc = new UserAccount
                {
                    EmployeeId = dlg.ResultEmployee.EmployeeId,
                    Username = dlg.ResultUsername.ToLower(),
                    PasswordHash = pwdHash,
                    PasswordSalt = salt,
                    IsActive = dlg.ResultEmployee.Status == "ACTIVE"
                };
                _db.UserAccounts.Add(userAcc);
                await _db.SaveChangesAsync();

                var assignment = new UserRoleBranchAssignment
                {
                    UserId = userAcc.UserId,
                    RoleId = dlg.ResultRoleId,
                    BranchId = dlg.ResultBranchId,
                    IsActive = true
                };
                _db.UserRoleBranchAssignments.Add(assignment);
                await _db.SaveChangesAsync();

                await LoadEmployeeManagementDataAsync();
                MessageBox.Show($"🎉 Đã thêm nhân viên '{dlg.ResultEmployee.FullName}' thành công!\n• Tài khoản: {dlg.ResultUsername}\n• Mật khẩu mặc định: 123456", "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var emp = await _db.Employees.FindAsync(empVm.EmployeeId);
            if (emp == null) return;

            var roles = await _db.Roles.ToListAsync();
            var branches = await _db.Branches.Where(b => b.IsActive).ToListAsync();

            var dlg = new EmployeeEditWindow(roles, branches, emp, empVm.Username, empVm.RoleId, empVm.BranchId);
            if (dlg.ShowDialog() == true)
            {
                string newUsername = dlg.ResultUsername.Trim().ToLower();

                // Kiểm tra trùng username với user khác
                if (await _db.UserAccounts.AnyAsync(u => u.Username.ToLower() == newUsername && (!empVm.UserId.HasValue || u.UserId != empVm.UserId.Value)))
                {
                    MessageBox.Show($"Tên đăng nhập '{newUsername}' đã được sử dụng bởi nhân viên khác!", "Trùng tên đăng nhập", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Tìm hoặc tạo UserAccount cho nhân viên
                UserAccount? user = null;
                if (empVm.UserId.HasValue)
                {
                    user = await _db.UserAccounts.FindAsync(empVm.UserId.Value);
                }
                if (user == null)
                {
                    user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.EmployeeId == emp.EmployeeId);
                }

                if (user == null)
                {
                    // Tạo tài khoản mới cho nhân viên nếu chưa có
                    user = new UserAccount
                    {
                        EmployeeId = emp.EmployeeId,
                        Username = newUsername,
                        PasswordHash = AuthService.HashMd5("123456"),
                        PasswordSalt = "SALT_CAFE_2026",
                        IsActive = emp.Status == "ACTIVE",
                        IsLocked = emp.Status != "ACTIVE"
                    };
                    _db.UserAccounts.Add(user);
                    await _db.SaveChangesAsync();
                }
                else
                {
                    user.Username = newUsername;
                    user.IsActive = emp.Status == "ACTIVE";
                    user.IsLocked = emp.Status != "ACTIVE";
                    if (dlg.ResetPasswordRequested)
                    {
                        user.PasswordHash = AuthService.HashMd5("123456");
                    }
                }

                // Xóa các phân quyền cũ của user này để gán 1 vai trò mới chuẩn xác, tránh vi phạm ràng buộc Unique
                var oldAssignments = await _db.UserRoleBranchAssignments.Where(a => a.UserId == user.UserId).ToListAsync();
                _db.UserRoleBranchAssignments.RemoveRange(oldAssignments);

                _db.UserRoleBranchAssignments.Add(new UserRoleBranchAssignment
                {
                    UserId = user.UserId,
                    RoleId = dlg.ResultRoleId,
                    BranchId = dlg.ResultBranchId,
                    IsActive = true,
                    AssignedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                await LoadEmployeeManagementDataAsync();
                MessageBox.Show($"🎉 Đã cập nhật hồ sơ nhân viên '{emp.FullName}' thành công!" + (dlg.ResetPasswordRequested ? "\n• Đã đặt lại mật khẩu về: 123456" : ""), "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var emp = await _db.Employees.FindAsync(empVm.EmployeeId);
            if (emp == null) return;

            emp.Status = emp.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";

            // Tìm user liên kết
            var user = empVm.UserId.HasValue
                ? await _db.UserAccounts.FindAsync(empVm.UserId.Value)
                : await _db.UserAccounts.FirstOrDefaultAsync(u => u.EmployeeId == emp.EmployeeId);

            if (user != null)
            {
                user.IsActive = emp.Status == "ACTIVE";
                user.IsLocked = emp.Status != "ACTIVE";
            }

            await _db.SaveChangesAsync();
            await LoadEmployeeManagementDataAsync();
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
                var user = await _db.UserAccounts.FindAsync(empVm.UserId.Value);
                if (user != null)
                {
                    user.PasswordHash = AuthService.HashMd5("123456");
                    await _db.SaveChangesAsync();
                    MessageBox.Show($"Đã đặt lại mật khẩu của '{empVm.Username}' về '123456' thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi đặt lại mật khẩu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
