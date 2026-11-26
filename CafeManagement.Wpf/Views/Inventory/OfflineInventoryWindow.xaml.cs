using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Wpf.Services.Http;
using CafeManagement.Wpf.Services.Offline;

namespace CafeManagement.Wpf.Views.Inventory;

public partial class OfflineInventoryWindow : Window
{
    private List<OfflineInventoryItemViewModel> _allItems = [];
    private OfflineInventoryItemViewModel? _selectedItem;

    public OfflineInventoryWindow()
    {
        InitializeComponent();
        Loaded += OfflineInventoryWindow_Loaded;
    }

    private void OfflineInventoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateNetworkStatusDisplay();
        LoadCachedInventory();
    }

    private void UpdateNetworkStatusDisplay()
    {
        bool isOnline = OfflineSyncManager.Instance.IsOnline;
        if (isOnline)
        {
            TxtNetworkStatus.Text = "🟢 ONLINE (Máy chủ sẵn sàng)";
            TxtNetworkStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
            BorderNetworkStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            BtnSyncFromServer.IsEnabled = true;
            BtnSyncFromServer.Opacity = 1.0;
        }
        else
        {
            TxtNetworkStatus.Text = "🟠 OFFLINE (Chế độ ngoại tuyến)";
            TxtNetworkStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C2410C"));
            BorderNetworkStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFEDD5"));
            BtnSyncFromServer.IsEnabled = false;
            BtnSyncFromServer.Opacity = 0.6;
        }
    }

    private void LoadCachedInventory()
    {
        var cache = OfflineStorageService.Instance.GetMenuCache();
        var inventories = cache?.Inventories ?? [];

        _allItems = inventories.Select(i => new OfflineInventoryItemViewModel
        {
            IngredientId = i.IngredientId,
            IngredientCode = i.IngredientCode,
            IngredientName = i.IngredientName,
            BaseUom = i.BaseUom,
            QuantityOnHand = i.QuantityOnHand,
            MinStockAlert = i.MinStockAlert
        }).OrderBy(i => i.IngredientName).ToList();

        string cachedTimeStr = cache != null ? cache.CachedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : "Chưa có";
        TxtCacheSummary.Text = $"Tổng: {_allItems.Count} nguyên liệu | Lưu lần cuối: {cachedTimeStr}";

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string kw = TxtSearch?.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(kw)
            ? _allItems
            : _allItems.Where(i => i.IngredientName.ToLower().Contains(kw) || i.IngredientCode.ToLower().Contains(kw)).ToList();

        DgCachedInventory.ItemsSource = new ObservableCollection<OfflineInventoryItemViewModel>(filtered);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void DgCachedInventory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedItem = DgCachedInventory.SelectedItem as OfflineInventoryItemViewModel;
        if (_selectedItem != null)
        {
            TxtSelectedIngredientLabel.Text = $"Đang chọn: {_selectedItem.IngredientName} (Tồn hiện tại: {_selectedItem.QuantityOnHand:N1} {_selectedItem.BaseUom})";
            TxtInputUom.Text = _selectedItem.BaseUom;
            TxtQuantityToAdd.Focus();
        }
        else
        {
            TxtSelectedIngredientLabel.Text = "(Vui lòng chọn 1 nguyên liệu ở bảng trên)";
            TxtInputUom.Text = "g";
        }
    }

    private decimal ParseInputQuantity(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        input = input.Trim().Replace(" ", "").Replace("x", "*").Replace("X", "*");

        if (input.Contains('*'))
        {
            var parts = input.Split('*', StringSplitOptions.RemoveEmptyEntries);
            decimal total = 1;
            foreach (var part in parts)
            {
                if (decimal.TryParse(part, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val) ||
                    decimal.TryParse(part, out val))
                {
                    total *= val;
                }
                else
                {
                    return 0;
                }
            }
            return total;
        }

        if (decimal.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal single) ||
            decimal.TryParse(input, out single))
        {
            return single;
        }

        return 0;
    }

    private void BtnAddStock_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null)
        {
            MessageBox.Show("Vui lòng nhấp chọn một nguyên liệu trong bảng trước khi nạp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal qty = ParseInputQuantity(TxtQuantityToAdd.Text);
        if (qty <= 0)
        {
            MessageBox.Show("Vui lòng nhập số lượng nạp hợp lệ lớn hơn 0 (VD: 5000 hoặc 330*24)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success = OfflineStorageService.Instance.UpdateCachedStock(_selectedItem.IngredientId, qty);
        if (success)
        {
            MessageBox.Show($"🎉 Đã nạp bổ sung thành công +{qty:N1} {_selectedItem.BaseUom} cho '{_selectedItem.IngredientName}' vào bộ nhớ đệm Offline!", 
                "Nạp Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            TxtQuantityToAdd.Text = "";
            LoadCachedInventory();

            // Re-select
            var updated = _allItems.FirstOrDefault(i => i.IngredientId == _selectedItem.IngredientId);
            if (updated != null)
            {
                DgCachedInventory.SelectedItem = updated;
            }
        }
        else
        {
            MessageBox.Show("Không thể cập nhật bộ nhớ đệm. Vui lòng kiểm tra lại quyền ghi file cục bộ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSetStock_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedItem == null)
        {
            MessageBox.Show("Vui lòng nhấp chọn một nguyên liệu trong bảng trước khi gán số lượng!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal qty = ParseInputQuantity(TxtQuantityToAdd.Text);
        if (qty < 0)
        {
            MessageBox.Show("Số lượng gán không thể là số âm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success = OfflineStorageService.Instance.SetCachedStock(_selectedItem.IngredientId, qty);
        if (success)
        {
            MessageBox.Show($"📝 Đã cập nhật tồn kho bộ nhớ đệm của '{_selectedItem.IngredientName}' thành {qty:N1} {_selectedItem.BaseUom}!", 
                "Cập Nhật Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            TxtQuantityToAdd.Text = "";
            LoadCachedInventory();

            var updated = _allItems.FirstOrDefault(i => i.IngredientId == _selectedItem.IngredientId);
            if (updated != null)
            {
                DgCachedInventory.SelectedItem = updated;
            }
        }
    }

    private void QuickAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && decimal.TryParse(tagStr, out decimal val))
        {
            decimal current = ParseInputQuantity(TxtQuantityToAdd.Text);
            TxtQuantityToAdd.Text = (current + val).ToString("0.##");
        }
    }

    private void QuickMultiply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && decimal.TryParse(tagStr, out decimal multiplier))
        {
            string current = TxtQuantityToAdd.Text.Trim();
            if (string.IsNullOrEmpty(current))
            {
                TxtQuantityToAdd.Text = $"1*{multiplier}";
            }
            else
            {
                TxtQuantityToAdd.Text = $"{current}*{multiplier}";
            }
        }
    }

    private async void BtnSyncFromServer_Click(object sender, RoutedEventArgs e)
    {
        if (!OfflineSyncManager.Instance.IsOnline)
        {
            MessageBox.Show("Không thể kết nối đến máy chủ API để đồng bộ. Vui lòng kiểm tra lại kết nối mạng hoặc VPN Tailscale.", "Chế độ Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            int branchId = 1; // Chi nhánh mặc định hoặc hiện tại
            var stocks = await InventoryApiClient.Instance.GetBranchInventoryAsync(branchId);
            if (stocks.Count > 0)
            {
                OfflineStorageService.Instance.SaveCachedInventories(stocks);
                MessageBox.Show($"✅ Đã đồng bộ thành công {stocks.Count} nguyên vật liệu từ máy chủ vào bộ nhớ đệm cục bộ!", 
                    "Đồng Bộ Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadCachedInventory();
            }
            else
            {
                MessageBox.Show("Máy chủ không trả về dữ liệu kho nào.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu kho từ máy chủ: {ex.Message}", "Lỗi Đồng Bộ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnToggleAddNew_Click(object sender, RoutedEventArgs e)
    {
        BorderAddNewForm.Visibility = BorderAddNewForm.Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }

    private void BtnCancelNew_Click(object sender, RoutedEventArgs e)
    {
        BorderAddNewForm.Visibility = Visibility.Collapsed;
    }

    private void BtnSaveNewIngredient_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtNewCode.Text.Trim().ToUpper();
        string name = TxtNewName.Text.Trim();
        string uom = string.IsNullOrWhiteSpace(TxtNewUom.Text) ? "g" : TxtNewUom.Text.Trim();
        decimal qty = ParseInputQuantity(TxtNewQuantity.Text);

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập đầy đủ Mã và Tên nguyên liệu mới!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var newDto = new BranchInventoryDto
        {
            IngredientId = 0,
            IngredientCode = code,
            IngredientName = name,
            BaseUom = uom,
            QuantityOnHand = Math.Max(0, qty),
            MinStockAlert = 500
        };

        bool ok = OfflineStorageService.Instance.AddOrUpdateCachedIngredient(newDto);
        if (ok)
        {
            MessageBox.Show($"🎉 Đã thêm nguyên liệu '{name}' vào bộ nhớ đệm với số lượng dự phòng {qty:N1} {uom}!", 
                "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            TxtNewCode.Text = "";
            TxtNewName.Text = "";
            TxtNewQuantity.Text = "10000";
            BorderAddNewForm.Visibility = Visibility.Collapsed;

            LoadCachedInventory();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class OfflineInventoryItemViewModel
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public string BaseUom { get; set; } = "g";
    public decimal QuantityOnHand { get; set; }
    public decimal MinStockAlert { get; set; }

    public bool IsOutOfStock => QuantityOnHand <= 0;
    public bool IsLowStock => QuantityOnHand <= MinStockAlert && QuantityOnHand > 0;

    public string StockColor => IsOutOfStock ? "#DC2626" : (IsLowStock ? "#D97706" : "#16A34A");

    public string StatusText => IsOutOfStock 
        ? "⚠️ Đã cạn đệm (0)" 
        : (IsLowStock ? "⚠️ Sắp hết trong đệm" : "✅ Sẵn sàng phục vụ");

    public string StatusColor => IsOutOfStock 
        ? "#B91C1C" 
        : (IsLowStock ? "#B45309" : "#15803D");

    public string StatusBackground => IsOutOfStock 
        ? "#FEE2E2" 
        : (IsLowStock ? "#FEF3C7" : "#DCFCE7");
}
