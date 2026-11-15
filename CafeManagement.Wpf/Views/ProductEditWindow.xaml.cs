using System.Windows;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class ProductEditWindow : Window
{
    public List<ProductCategory> Categories { get; }
    public Product? ProductToSave { get; private set; }
    public decimal ExtraPriceSizeL { get; private set; } = 6000;
    public bool IsNewProduct { get; }

    public ProductEditWindow(List<ProductCategory> categories, Product? existing = null)
    {
        InitializeComponent();
        Categories = categories;
        CboCategory.ItemsSource = categories;

        if (existing == null)
        {
            IsNewProduct = true;
            TxtTitle.Text = "➕ THÊM MÓN NƯỚC MỚI VÀO THỰC ĐƠN";
            if (categories.Count > 0) CboCategory.SelectedIndex = 0;
            TxtProductCode.Text = $"CF_{Random.Shared.Next(100, 999)}";
            TxtBasePrice.Text = "30000";
            TxtExtraPriceL.Text = "6000";
        }
        else
        {
            IsNewProduct = false;
            ProductToSave = existing;
            TxtTitle.Text = $"✏️ CHỈNH SỬA MÓN: {existing.ProductName}";

            var cat = categories.FirstOrDefault(c => c.CategoryId == existing.CategoryId);
            if (cat != null) CboCategory.SelectedItem = cat;
            else if (categories.Count > 0) CboCategory.SelectedIndex = 0;

            TxtProductCode.Text = existing.ProductCode;
            TxtProductCode.IsEnabled = false; // Không sửa mã món để tránh đứt gãy quan hệ
            TxtProductName.Text = existing.ProductName;
            TxtBasePrice.Text = existing.BasePrice.ToString("N0");
            TxtDescription.Text = existing.Description ?? "";
            ChkIsActive.IsChecked = existing.IsActive;

            var variantL = existing.Variants.FirstOrDefault(v => v.Size == "L");
            TxtExtraPriceL.Text = (variantL?.ExtraPrice ?? 6000).ToString("N0");
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (CboCategory.SelectedItem is not ProductCategory cat)
        {
            MessageBox.Show("Vui lòng chọn danh mục cho món!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string code = TxtProductCode.Text.Trim();
        string name = TxtProductName.Text.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập đầy đủ mã món và tên món!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string rawBasePrice = TxtBasePrice.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawBasePrice, out decimal basePrice) || basePrice <= 0)
        {
            MessageBox.Show("Giá bán cơ bản phải là số lớn hơn 0!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string rawExtraL = TxtExtraPriceL.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawExtraL, out decimal extraL) || extraL < 0)
        {
            MessageBox.Show("Phụ thu Size L không hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ExtraPriceSizeL = extraL;

        if (IsNewProduct || ProductToSave == null)
        {
            ProductToSave = new Product
            {
                CategoryId = cat.CategoryId,
                ProductCode = code,
                ProductName = name,
                BasePrice = basePrice,
                Description = TxtDescription.Text.Trim(),
                IsActive = ChkIsActive.IsChecked == true
            };

            // Tạo sẵn 2 biến thể Size M và Size L
            ProductToSave.Variants.Add(new ProductVariant
            {
                SkuCode = $"{code}_M",
                Size = "M",
                Temperature = "ICED",
                ExtraPrice = 0,
                IsActive = true
            });

            ProductToSave.Variants.Add(new ProductVariant
            {
                SkuCode = $"{code}_L",
                Size = "L",
                Temperature = "ICED",
                ExtraPrice = extraL,
                IsActive = true
            });
        }
        else
        {
            ProductToSave.CategoryId = cat.CategoryId;
            ProductToSave.ProductName = name;
            ProductToSave.BasePrice = basePrice;
            ProductToSave.Description = TxtDescription.Text.Trim();
            ProductToSave.IsActive = ChkIsActive.IsChecked == true;

            var variantL = ProductToSave.Variants.FirstOrDefault(v => v.Size == "L");
            if (variantL != null)
            {
                variantL.ExtraPrice = extraL;
            }
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
