using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Core.DTOs.Procurement;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Procurement;

public class CreatePoItemRow
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal OrderedPackageQuantity { get; set; } // Số lượng đơn vị đóng gói đặt (vd: 10 bịch, 2 bao)
    public string PackageUom { get; set; } = "Gói"; // vd: "Bao 25kg", "Bịch 1kg"
    public decimal ConversionFactor { get; set; } = 1.0m; // vd: 25000, 1000
    public decimal OrderedQuantity => OrderedPackageQuantity * (ConversionFactor > 0 ? ConversionFactor : 1.0m); // Số lượng BaseUom
    public string BaseUom { get; set; } = "g";
    public string BaseQuantityDisplay => $"= {OrderedQuantity:N0} {BaseUom}";
    public decimal UnitPrice { get; set; } // Giá 1 đơn vị đóng gói (vd: 150.000 đ/bịch)
    public decimal SubTotal => OrderedPackageQuantity * UnitPrice; // Thành tiền
    public string Uom => PackageUom;
}

public partial class CreatePurchaseOrderWindow : Window
{
    public CreatePurchaseOrderRequest RequestData { get; private set; } = new();

    private readonly List<SupplierDto> _suppliers;
    private readonly List<BranchDto> _branches;
    private readonly int _currentUserId;

    private List<SupplierIngredientDto> _currentSupplierIngredients = [];
    private readonly ObservableCollection<CreatePoItemRow> _items = [];

    public CreatePurchaseOrderWindow(List<SupplierDto> suppliers, List<BranchDto> branches, int defaultBranchId, int currentUserId)
    {
        InitializeComponent();

        _suppliers = suppliers;
        _branches = branches;
        _currentUserId = currentUserId;

        CboSupplier.ItemsSource = _suppliers;
        CboBranch.ItemsSource = _branches;

        if (_suppliers.Count > 0) CboSupplier.SelectedIndex = 0;

        var selectedBranch = _branches.FirstOrDefault(b => b.BranchId == defaultBranchId);
        if (selectedBranch != null)
        {
            CboBranch.SelectedItem = selectedBranch;
        }
        else if (_branches.Count > 0)
        {
            CboBranch.SelectedIndex = 0;
        }

        DpExpectedDelivery.SelectedDate = DateTime.Today.AddDays(2);
        DgPoItems.ItemsSource = _items;
    }

    private async void CboSupplier_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboSupplier.SelectedValue is not int supplierId || supplierId <= 0) return;

        _currentSupplierIngredients = await ProcurementApiClient.Instance.GetSupplierIngredientsAsync(supplierId);
        CboSupplierIngredient.ItemsSource = _currentSupplierIngredients;

        if (_currentSupplierIngredients.Count > 0)
        {
            CboSupplierIngredient.SelectedIndex = 0;
        }
        else
        {
            TxtUnitPrice.Text = "0";
            TxtPackageHint.Text = "Nhà cung cấp này chưa có mặt hàng nào trong bảng giá.";
        }
    }

    private void CboSupplierIngredient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboSupplierIngredient.SelectedItem is SupplierIngredientDto si)
        {
            TxtUnitPrice.Text = si.SupplyPrice.ToString("F0");
            TxtOrderQty.Text = si.MinOrderQuantity > 0 ? si.MinOrderQuantity.ToString("G") : "1";
            LblOrderQty.Text = $"SL ĐẶT ({si.PackageUom.ToUpper()})";
            LblUnitPrice.Text = $"ĐƠN GIÁ / {si.PackageUom.ToUpper()}";

            UpdateItemHint(si);
        }
    }

    private void OrderQty_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CboSupplierIngredient?.SelectedItem is SupplierIngredientDto si)
        {
            UpdateItemHint(si);
        }
    }

    private void UpdateItemHint(SupplierIngredientDto si)
    {
        if (TxtPackageHint == null) return;

        decimal qty = ParseSmartNumber(TxtOrderQty.Text);
        decimal price = ParseSmartNumber(TxtUnitPrice.Text);
        decimal factor = si.ConversionFactor > 0 ? si.ConversionFactor : 1.0m;
        decimal baseQty = qty * factor;
        decimal subTotal = qty * price;

        TxtPackageHint.Text = $"💡 Quy cách NCC: {si.PackageUom} (= {factor:N0} {si.BaseUom}) | Đặt: {qty:G} {si.PackageUom} (= {baseQty:N0} {si.BaseUom}) | Thành tiền: {subTotal:N0} đ";
    }

    private void AddItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (CboSupplierIngredient.SelectedItem is not SupplierIngredientDto si)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu từ bảng giá nhà cung cấp.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal qty = ParseSmartNumber(TxtOrderQty.Text);
        if (qty <= 0)
        {
            MessageBox.Show("Số lượng đặt phải lớn hơn 0.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtOrderQty.Focus();
            return;
        }

        decimal price = ParseSmartNumber(TxtUnitPrice.Text);
        if (price < 0)
        {
            MessageBox.Show("Đơn giá không hợp lệ.", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtUnitPrice.Focus();
            return;
        }

        // Kiểm tra xem đã có món này trong đơn chưa
        var existing = _items.FirstOrDefault(i => i.IngredientId == si.IngredientId);
        if (existing != null)
        {
            existing.OrderedPackageQuantity += qty;
            existing.UnitPrice = price;
        }
        else
        {
            _items.Add(new CreatePoItemRow
            {
                IngredientId = si.IngredientId,
                IngredientCode = si.IngredientCode,
                IngredientName = si.IngredientName,
                OrderedPackageQuantity = qty,
                PackageUom = si.PackageUom,
                ConversionFactor = si.ConversionFactor > 0 ? si.ConversionFactor : 1.0m,
                BaseUom = si.BaseUom,
                UnitPrice = price
            });
        }

        DgPoItems.Items.Refresh();
        UpdateTotals();
    }

    private void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CreatePoItemRow item)
        {
            _items.Remove(item);
            UpdateTotals();
        }
    }

    private void CalculateTotal_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        if (TxtDiscount == null || TxtTax == null || TxtSubTotalDisplay == null || TxtTotalAmountDisplay == null)
            return;

        decimal subTotal = _items.Sum(i => i.SubTotal);
        decimal discount = ParseSmartNumber(TxtDiscount.Text);
        decimal tax = ParseSmartNumber(TxtTax.Text);

        decimal total = Math.Max(0, subTotal - discount + tax);

        TxtSubTotalDisplay.Text = $"{subTotal:N0} đ";
        TxtTotalAmountDisplay.Text = $"{total:N0} đ";
    }

    private void SubmitPoButton_Click(object sender, RoutedEventArgs e)
    {
        if (CboSupplier.SelectedValue is not int supplierId || supplierId <= 0)
        {
            MessageBox.Show("Vui lòng chọn Nhà cung cấp.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CboBranch.SelectedValue is not int branchId || branchId <= 0)
        {
            MessageBox.Show("Vui lòng chọn Chi nhánh nhận hàng.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_items.Count == 0)
        {
            MessageBox.Show("Đơn đặt hàng phải có ít nhất 1 mặt hàng nguyên liệu.", "Thiếu dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal discount = ParseSmartNumber(TxtDiscount.Text);
        decimal tax = ParseSmartNumber(TxtTax.Text);

        RequestData = new CreatePurchaseOrderRequest
        {
            SupplierId = supplierId,
            BranchId = branchId,
            ExpectedDeliveryDate = DpExpectedDelivery.SelectedDate,
            CreatedByUserId = _currentUserId,
            DiscountAmount = discount,
            TaxAmount = tax,
            Note = TxtNote.Text.Trim(),
            Items = _items.Select(i => new CreatePurchaseOrderItemRequest
            {
                IngredientId = i.IngredientId,
                // Lưu số lượng BaseUom (g/ml) chuẩn kho
                OrderedQuantity = i.OrderedQuantity,
                // Đơn giá tính theo 1 đơn vị cơ sở BaseUom để TotalAmount và thẻ kho khớp tuyệt đối
                UnitPrice = i.ConversionFactor > 0 ? (i.UnitPrice / i.ConversionFactor) : i.UnitPrice,
                Uom = i.BaseUom
            }).ToList()
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    public static decimal ParseSmartNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string clean = text.Trim().Replace(" ", "");

        // Hỗ trợ kiểu phân cách hàng nghìn tiếng Việt: 10.000 hoặc 150.000
        if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\d{1,3}(\.\d{3})+$"))
        {
            clean = clean.Replace(".", "");
        }
        else if (System.Text.RegularExpressions.Regex.IsMatch(clean, @"^\d{1,3}(,\d{3})+$"))
        {
            clean = clean.Replace(",", "");
        }
        else
        {
            clean = clean.Replace(",", ".");
        }

        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val))
        {
            return val;
        }
        return 0;
    }
}
