using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using CafeManagement.Wpf.Data;
using CafeManagement.Wpf.Models;
using CafeManagement.Wpf.Views;
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
    private UserSession _currentSession = new();

    public bool CanAccessPos => CurrentSession.CanAccessPos;
    public bool CanAccessKds => CurrentSession.CanAccessKds;
    public bool CanAccessInventory => CurrentSession.CanAccessInventory;

    public event Action? RequestLogout;

    // 1. ĐIỀU HƯỚNG GIAO DIỆN (NAVIGATION)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPosView))]
    [NotifyPropertyChangedFor(nameof(IsKdsView))]
    [NotifyPropertyChangedFor(nameof(IsInventoryView))]
    private string _currentView = "POS"; // "POS", "KDS", "INVENTORY", "PRODUCT", "REPORT"

    public bool IsPosView => CurrentView == "POS";
    public bool IsKdsView => CurrentView == "KDS";
    public bool IsInventoryView => CurrentView == "INVENTORY";

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

    public decimal FinalAmount => SubTotal - DiscountAmount;

    [ObservableProperty]
    private string _orderNumber = GenerateOrderNumber();

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
    private int _lowStockCount = 0;

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
                    $"• Biên nhận: {receipt.ReceiptNumber}\n\n" +
                    $"⚡ Đã tự động trừ nguyên liệu trong kho và đẩy sang Quầy Barista!",
                    "Hoàn tất đơn hàng", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearCart();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi trong quá trình thanh toán: {ex.Message}\n\nChi tiết:\n{ex}", "Lỗi Thanh Toán", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

            // Tải 20 giao dịch Thẻ kho gần nhất
            var trans = await _db.InventoryTransactions
                .Include(t => t.Ingredient)
                .Where(t => t.BranchId == branchId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            var transItems = trans.Select(t => new InventoryTransactionItemViewModel
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

            InventoryTransactions = new ObservableCollection<InventoryTransactionItemViewModel>(transItems);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu kho: {ex.Message}", "Lỗi Kho", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
}
