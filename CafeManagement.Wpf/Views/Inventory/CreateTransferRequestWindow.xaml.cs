using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Inventory;

public class CreateTransferItemViewModel
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Uom { get; set; } = "g";
}

public partial class CreateTransferRequestWindow : Window
{
    private readonly UserSession _session;
    private List<Ingredient> _allIngredients = [];
    public ObservableCollection<CreateTransferItemViewModel> Items { get; } = [];
    public CreateStockTransferRequest? ResultRequest { get; private set; }

    public CreateTransferRequestWindow(UserSession session)
    {
        InitializeComponent();
        _session = session;
        GridAddedItems.ItemsSource = Items;
        Loaded += CreateTransferRequestWindow_Loaded;
    }

    private async void CreateTransferRequestWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var branches = (await AuthenticationApiClient.Instance.GetBranchesAsync()).Where(b => b.IsActive).ToList();
            var ingredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: true);
            _allIngredients = ingredients;

            CboFromBranch.ItemsSource = branches;
            CboToBranch.ItemsSource = branches.ToList();

            // Mặc định: Chi nhánh đích là chi nhánh hiện tại của người dùng
            if (_session.BranchId > 0)
            {
                CboToBranch.SelectedValue = _session.BranchId;
                // Chi nhánh nguồn chọn chi nhánh đầu tiên khác chi nhánh đích
                var otherBranch = branches.FirstOrDefault(b => b.BranchId != _session.BranchId);
                if (otherBranch != null)
                {
                    CboFromBranch.SelectedValue = otherBranch.BranchId;
                }
            }
            else if (branches.Count >= 2)
            {
                CboFromBranch.SelectedIndex = 0;
                CboToBranch.SelectedIndex = 1;
            }

            CboIngredient.ItemsSource = _allIngredients;
            if (_allIngredients.Count > 0)
            {
                CboIngredient.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu chi nhánh và nguyên liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CboIngredient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboIngredient.SelectedItem is Ingredient ing)
        {
            TxtUom.Text = $"(ĐVT: {ing.BaseUom})";
        }
    }

    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        if (CboIngredient.SelectedItem is not Ingredient ing)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu cần chuyển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtQuantity.Text.Trim(), out decimal qty) || qty <= 0)
        {
            MessageBox.Show("Số lượng chuyển phải là số dương lớn hơn 0!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtQuantity.Focus();
            return;
        }

        // Kiểm tra xem đã có trong bảng chưa, nếu có thì cộng dồn
        var existing = Items.FirstOrDefault(i => i.IngredientId == ing.IngredientId);
        if (existing != null)
        {
            existing.Quantity += qty;
            GridAddedItems.Items.Refresh();
        }
        else
        {
            Items.Add(new CreateTransferItemViewModel
            {
                IngredientId = ing.IngredientId,
                IngredientCode = ing.IngredientCode,
                IngredientName = ing.IngredientName,
                Quantity = qty,
                Uom = ing.BaseUom
            });
        }

        TxtQuantity.Text = string.Empty;
        TxtItemCount.Text = $"Đã chọn: {Items.Count} nguyên liệu";
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is CreateTransferItemViewModel item)
        {
            Items.Remove(item);
            TxtItemCount.Text = $"Đã chọn: {Items.Count} nguyên liệu";
        }
    }

    private void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        if (CboFromBranch.SelectedValue is not int fromBranchId || fromBranchId <= 0)
        {
            MessageBox.Show("Vui lòng chọn chi nhánh xuất hàng (nguồn)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CboToBranch.SelectedValue is not int toBranchId || toBranchId <= 0)
        {
            MessageBox.Show("Vui lòng chọn chi nhánh nhận hàng (đích)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (fromBranchId == toBranchId)
        {
            MessageBox.Show("Chi nhánh xuất và Chi nhánh nhận không thể trùng nhau!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Items.Count == 0)
        {
            MessageBox.Show("Vui lòng thêm ít nhất một nguyên liệu vào phiếu điều chuyển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultRequest = new CreateStockTransferRequest
        {
            FromBranchId = fromBranchId,
            ToBranchId = toBranchId,
            RequestNote = TxtNote.Text.Trim(),
            Items = Items.Select(i => new CreateTransferItemRequest
            {
                IngredientId = i.IngredientId,
                Quantity = i.Quantity,
                Uom = i.Uom
            }).ToList()
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
