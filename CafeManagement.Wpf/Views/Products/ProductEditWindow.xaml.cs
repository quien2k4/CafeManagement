using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.Products;

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
            UpdateImagePreview(null);
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

            TxtImageUrl.Text = existing.ImageUrl ?? "";
            UpdateImagePreview(existing.ImageUrl);
        }
    }

    private void UpdateImagePreview(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            ImgPreview.Source = null;
            PnlPlaceholder.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri))
            {
                bitmap.UriSource = uri;
            }
            else if (File.Exists(pathOrUrl))
            {
                bitmap.UriSource = new Uri(Path.GetFullPath(pathOrUrl));
            }
            else
            {
                ImgPreview.Source = null;
                PnlPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            bitmap.EndInit();
            ImgPreview.Source = bitmap;
            PnlPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ImgPreview.Source = null;
            PnlPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Chọn hình ảnh thực tế của món nước",
                Filter = "Tệp hình ảnh (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp|Tất cả tệp (*.*)|*.*",
                Multiselect = false
            };

            if (openDlg.ShowDialog() == true)
            {
                string sourceFile = openDlg.FileName;
                string productCode = TxtProductCode.Text.Trim();
                if (string.IsNullOrEmpty(productCode)) productCode = "prod";

                string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ProductImages");
                if (!Directory.Exists(imagesDir))
                {
                    Directory.CreateDirectory(imagesDir);
                }

                string ext = Path.GetExtension(sourceFile);
                string destFileName = $"{productCode}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                string destPath = Path.Combine(imagesDir, destFileName);

                File.Copy(sourceFile, destPath, overwrite: true);

                TxtImageUrl.Text = destPath;
                UpdateImagePreview(destPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi tải hình ảnh: {ex.Message}", "Lỗi Hình Ảnh", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClearImage_Click(object sender, RoutedEventArgs e)
    {
        TxtImageUrl.Text = string.Empty;
        ImgPreview.Source = null;
        PnlPlaceholder.Visibility = Visibility.Visible;
    }

    private void TxtImageUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateImagePreview(TxtImageUrl.Text.Trim());
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
        string imageVal = TxtImageUrl.Text.Trim();

        if (IsNewProduct || ProductToSave == null)
        {
            ProductToSave = new Product
            {
                CategoryId = cat.CategoryId,
                ProductCode = code,
                ProductName = name,
                BasePrice = basePrice,
                Description = TxtDescription.Text.Trim(),
                ImageUrl = string.IsNullOrWhiteSpace(imageVal) ? null : imageVal,
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
            ProductToSave.ImageUrl = string.IsNullOrWhiteSpace(imageVal) ? null : imageVal;
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
