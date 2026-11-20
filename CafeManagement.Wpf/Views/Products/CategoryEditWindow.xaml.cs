using System.Windows;
using CafeManagement.Core.DTOs.Categories;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Products;

public partial class CategoryEditWindow : Window
{
    private readonly ProductCategory? _editingCategory;

    public CategoryEditWindow(ProductCategory? category = null)
    {
        InitializeComponent();
        _editingCategory = category;

        if (_editingCategory != null)
        {
            TxtTitle.Text = "📁 CHỈNH SỬA DANH MỤC MÓN";
            TxtCategoryCode.Text = _editingCategory.CategoryCode;
            TxtCategoryCode.IsEnabled = false; // Không cho sửa mã khi edit
            TxtCategoryName.Text = _editingCategory.CategoryName;
            TxtDisplayOrder.Text = _editingCategory.DisplayOrder.ToString();
            ChkIsActive.IsChecked = _editingCategory.IsActive;
        }
        else
        {
            TxtTitle.Text = "➕ THÊM DANH MỤC MÓN MỚI";
            TxtCategoryCode.Text = "CAT_" + DateTime.Now.ToString("HHmmss");
            TxtCategoryName.Text = string.Empty;
            TxtDisplayOrder.Text = "1";
            ChkIsActive.IsChecked = true;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtCategoryCode.Text.Trim().ToUpper();
        string name = TxtCategoryName.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("Vui lòng nhập mã danh mục!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lòng nhập tên danh mục!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int.TryParse(TxtDisplayOrder.Text.Trim(), out int displayOrder);
        bool isActive = ChkIsActive.IsChecked == true;

        BtnSave.IsEnabled = false;
        BtnSave.Content = "Đang lưu...";

        try
        {
            if (_editingCategory == null)
            {
                var req = new CategoryCreateRequest
                {
                    CategoryCode = code,
                    CategoryName = name,
                    DisplayOrder = displayOrder,
                    IsActive = isActive
                };

                var res = await ProductApiClient.Instance.CreateCategoryAsync(req);
                if (res != null)
                {
                    MessageBox.Show($"Thêm danh mục '{res.CategoryName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            else
            {
                var req = new CategoryUpdateRequest
                {
                    CategoryName = name,
                    DisplayOrder = displayOrder,
                    IsActive = isActive
                };

                var res = await ProductApiClient.Instance.UpdateCategoryAsync(_editingCategory.CategoryId, req);
                if (res != null)
                {
                    MessageBox.Show($"Cập nhật danh mục '{res.CategoryName}' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi lưu danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSave.IsEnabled = true;
            BtnSave.Content = "💾 Lưu Danh Mục";
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
