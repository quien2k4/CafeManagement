using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Procurement;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.Procurement;

public partial class SupplierIngredientEditWindow : Window
{
    public SetSupplierIngredientRequest RequestData { get; private set; } = new();
    private readonly int _supplierId;
    private readonly SupplierIngredientDto? _existing;

    public SupplierIngredientEditWindow(int supplierId, List<Ingredient> allIngredients, SupplierIngredientDto? existing = null)
    {
        InitializeComponent();

        _supplierId = supplierId;
        _existing = existing;
        CboIngredient.ItemsSource = allIngredients;

        if (existing != null)
        {
            TxtHeader.Text = $"🏷️ CẬP NHẬT GIÁ: {existing.IngredientName}";
            CboIngredient.SelectedValue = existing.IngredientId;
            CboIngredient.IsEnabled = false;

            TxtPackagePrice.Text = existing.SupplyPrice.ToString("F0");
            TxtMinOrderQty.Text = existing.MinOrderQuantity.ToString("G");
            TxtLeadTime.Text = existing.LeadTimeDays.ToString();
            ChkIsPreferred.IsChecked = existing.IsPreferred;

            // Phân tích quy cách cũ nếu có (ví dụ "Bao 25kg")
            ParseExistingPackageUom(existing.PackageUom, existing.ConversionFactor);
        }
        else
        {
            if (allIngredients.Count > 0)
            {
                CboIngredient.SelectedIndex = 0;
            }
        }

        UpdateCalculationPreview();
    }

    private void ParseExistingPackageUom(string packageUom, decimal factor)
    {
        if (string.IsNullOrWhiteSpace(packageUom)) return;

        // Nếu chuỗi có định dạng như "Bao 25kg" hoặc "Thùng 12 hộp"
        string[] parts = packageUom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            foreach (ComboBoxItem item in CboPackageType.Items)
            {
                if (string.Equals(item.Content.ToString(), parts[0], StringComparison.OrdinalIgnoreCase))
                {
                    CboPackageType.SelectedItem = item;
                    break;
                }
            }
        }

        if (factor >= 1000)
        {
            TxtPackageSize.Text = (factor / 1000m).ToString("G");
            SelectWeightUnit("kg");
        }
        else
        {
            TxtPackageSize.Text = factor.ToString("G");
            SelectWeightUnit("g");
        }
    }

    private void SelectWeightUnit(string key)
    {
        foreach (ComboBoxItem item in CboWeightUnit.Items)
        {
            if (item.Content.ToString()!.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                CboWeightUnit.SelectedItem = item;
                break;
            }
        }
    }

    private void CboIngredient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboIngredient.SelectedItem is Ingredient ing)
        {
            // Tự động gợi ý đơn vị phù hợp theo BaseUom của kho
            if (ing.BaseUom.Equals("ml", StringComparison.OrdinalIgnoreCase))
            {
                SelectWeightUnit("lít");
            }
            else if (ing.BaseUom.Equals("g", StringComparison.OrdinalIgnoreCase))
            {
                SelectWeightUnit("kg");
            }
            else
            {
                SelectWeightUnit("cái");
            }
        }
        UpdateCalculationPreview();
    }

    private void Recalculate_Changed(object sender, RoutedEventArgs e)
    {
        UpdateCalculationPreview();
    }

    private void UpdateCalculationPreview()
    {
        if (TxtPreviewCalculation == null || CboPackageType == null || TxtPackageSize == null || CboWeightUnit == null || TxtPackagePrice == null)
            return;

        string packageType = (CboPackageType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Bịch";
        string baseUom = (CboIngredient.SelectedItem as Ingredient)?.BaseUom ?? "g";

        decimal size = ParseSmartNumber(TxtPackageSize.Text);
        if (size <= 0) size = 1;

        decimal multiplier = 1000;
        string unitText = "kg";
        if (CboWeightUnit.SelectedItem is ComboBoxItem unitItem)
        {
            decimal.TryParse(unitItem.Tag?.ToString(), out multiplier);
            if (multiplier <= 0) multiplier = 1;
            unitText = unitItem.Content.ToString()!.Split(' ')[0];
        }

        decimal totalBaseUnits = size * multiplier;
        decimal price = ParseSmartNumber(TxtPackagePrice.Text);

        string unitPriceDisplay = "";
        if (totalBaseUnits > 0 && price > 0)
        {
            if (multiplier == 1000)
            {
                decimal pricePerKg = price / size;
                unitPriceDisplay = $" (Tương đương: {pricePerKg:N0} đ/{unitText})";
            }
        }

        TxtPreviewCalculation.Text = $"👉 Quy cách: {packageType} {size:G}{unitText} (= {totalBaseUnits:N0} {baseUom}) | Giá: {price:N0} đ / {packageType}{unitPriceDisplay}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (CboIngredient.SelectedValue is not int ingredientId || ingredientId <= 0)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu cần thiết lập bảng giá.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal price = ParseSmartNumber(TxtPackagePrice.Text);
        if (price <= 0)
        {
            MessageBox.Show("Vui lòng nhập giá mua của 1 đơn vị đóng gói (lớn hơn 0).", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPackagePrice.Focus();
            return;
        }

        string packageType = (CboPackageType.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Bịch";
        decimal size = ParseSmartNumber(TxtPackageSize.Text);
        if (size <= 0) size = 1;

        decimal multiplier = 1000;
        string unitText = "kg";
        if (CboWeightUnit.SelectedItem is ComboBoxItem unitItem)
        {
            decimal.TryParse(unitItem.Tag?.ToString(), out multiplier);
            if (multiplier <= 0) multiplier = 1;
            unitText = unitItem.Content.ToString()!.Split(' ')[0];
        }

        decimal factor = size * multiplier;
        string packageUom = $"{packageType} {size:G}{unitText}".Trim();

        decimal minQty = ParseSmartNumber(TxtMinOrderQty.Text);
        if (minQty <= 0) minQty = 1;

        int.TryParse(TxtLeadTime.Text.Trim(), out int leadTime);
        if (leadTime < 0) leadTime = 1;

        RequestData = new SetSupplierIngredientRequest
        {
            SupplierId = _supplierId,
            IngredientId = ingredientId,
            SupplyPrice = price, // Giá của 1 đơn vị đóng gói (vd: 150.000 đ/bịch)
            PackageUom = packageUom, // Vd: "Bịch 1kg", "Bao 25kg"
            ConversionFactor = factor, // Vd: 1000 hoặc 25000
            MinOrderQuantity = minQty,
            LeadTimeDays = leadTime,
            IsPreferred = ChkIsPreferred.IsChecked ?? false
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
