using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Recipes;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Services.Http;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views.Recipes;

public partial class RecipeConfigWindow : Window
{
    private readonly Product _product;
    private readonly List<Ingredient> _availableIngredients;
    private readonly ObservableCollection<RecipeDetailItemViewModel> _currentItems = [];

    public RecipeConfigWindow(Product product, List<Ingredient> ingredients)
    {
        InitializeComponent();
        _product = product;
        _availableIngredients = ingredients;

        TxtProductTitle.Text = $"⚙️ CÔNG THỨC: {_product.ProductName}";
        CboAvailableIngredients.ItemsSource = _availableIngredients;
        if (_availableIngredients.Count > 0)
        {
            CboAvailableIngredients.SelectedIndex = 0;
        }

        LstRecipeItems.ItemsSource = _currentItems;

        _ = LoadRecipeForSelectedSizeAsync();
    }

    private string SelectedSize => (CboSize.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Contains("L") == true ? "L" : "M";

    private async Task LoadRecipeForSelectedSizeAsync()
    {
        try
        {
            _currentItems.Clear();

            var variant = _product.Variants.FirstOrDefault(v => v.Size == SelectedSize);
            if (variant == null) return;

            var recipe = await RecipeApiClient.Instance.GetCurrentRecipeByVariantIdAsync(variant.VariantId);

            if (recipe != null)
            {
                foreach (var detail in recipe.Details)
                {
                    _currentItems.Add(new RecipeDetailItemViewModel
                    {
                        IngredientId = detail.IngredientId,
                        IngredientCode = detail.IngredientCode,
                        IngredientName = detail.IngredientName,
                        Quantity = detail.Quantity,
                        Uom = detail.Uom
                    });
                }
            }

            TxtCount.Text = $"({_currentItems.Count} nguyên liệu)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải công thức: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CboSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            _ = LoadRecipeForSelectedSizeAsync();
        }
    }

    private void CboAvailableIngredients_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboAvailableIngredients.SelectedItem is Ingredient ing)
        {
            TxtIngredientUom.Text = ing.BaseUom;
        }
    }

    private void AddIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (CboAvailableIngredients.SelectedItem is not Ingredient ing)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu cần thêm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string rawQty = TxtIngredientQty.Text.Replace(",", ".").Trim();
        if (!decimal.TryParse(rawQty, out decimal qty) || qty <= 0)
        {
            MessageBox.Show("Vui lòng nhập định mức định lượng hợp lệ (> 0)!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtIngredientQty.Focus();
            return;
        }

        var existing = _currentItems.FirstOrDefault(i => i.IngredientId == ing.IngredientId);
        if (existing != null)
        {
            existing.Quantity = qty;
            LstRecipeItems.Items.Refresh();
        }
        else
        {
            _currentItems.Add(new RecipeDetailItemViewModel
            {
                IngredientId = ing.IngredientId,
                IngredientCode = ing.IngredientCode,
                IngredientName = ing.IngredientName,
                Quantity = qty,
                Uom = ing.BaseUom
            });
        }

        TxtCount.Text = $"({_currentItems.Count} nguyên liệu)";
        TxtIngredientQty.Text = "";
        TxtIngredientQty.Focus();
    }

    private void RemoveIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RecipeDetailItemViewModel item)
        {
            _currentItems.Remove(item);
            TxtCount.Text = $"({_currentItems.Count} nguyên liệu)";
        }
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        if (_currentItems.Count == 0) return;

        var ask = MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ định mức nguyên liệu của size này?",
            "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (ask == MessageBoxResult.Yes)
        {
            _currentItems.Clear();
            TxtCount.Text = $"(0 nguyên liệu)";
        }
    }

    private async void SaveRecipe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var variant = _product.Variants.FirstOrDefault(v => v.Size == SelectedSize);
            if (variant == null)
            {
                MessageBox.Show($"Không tìm thấy kích cỡ {SelectedSize} của món này!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var saveReq = new SaveRecipeRequest
            {
                VariantId = variant.VariantId,
                Note = $"Cập nhật định mức ngày {DateTime.Now:dd/MM/yyyy HH:mm}",
                Details = _currentItems.Select(i => new SaveRecipeDetailItem
                {
                    IngredientId = i.IngredientId,
                    Quantity = i.Quantity,
                    Uom = i.Uom
                }).ToList()
            };

            await RecipeApiClient.Instance.SaveRecipeAsync(saveReq);

            MessageBox.Show($"🎉 Đã lưu công thức pha chế thành công cho {_product.ProductName} ({SelectedSize})!", 
                "Lưu Công Thức Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi lưu công thức qua API: {ex.Message}", "Lỗi API", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
