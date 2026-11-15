using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using CafeManagement.Wpf.Data;
using CafeManagement.Wpf.Models;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views;

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

            using var db = new CafeDbContext();
            var recipeVersion = await db.RecipeVersions
                .Include(rv => rv.Details)
                    .ThenInclude(d => d.Ingredient)
                .Where(rv => rv.Recipe!.VariantId == variant.VariantId && rv.IsCurrent)
                .FirstOrDefaultAsync();

            if (recipeVersion != null)
            {
                foreach (var detail in recipeVersion.Details)
                {
                    if (detail.Ingredient != null)
                    {
                        _currentItems.Add(new RecipeDetailItemViewModel
                        {
                            IngredientId = detail.IngredientId,
                            IngredientCode = detail.Ingredient.IngredientCode,
                            IngredientName = detail.Ingredient.IngredientName,
                            Quantity = detail.Quantity,
                            Uom = detail.Uom
                        });
                    }
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
            MessageBox.Show("Vui lòng nhập định lượng hợp lệ (lớn hơn 0)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = _currentItems.FirstOrDefault(i => i.IngredientId == ing.IngredientId);
        if (existing != null)
        {
            existing.Quantity = qty;
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
    }

    private void RemoveIngredient_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RecipeDetailItemViewModel item)
        {
            _currentItems.Remove(item);
            TxtCount.Text = $"({_currentItems.Count} nguyên liệu)";
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

            using var db = new CafeDbContext();

            // Tìm hoặc tạo Recipe
            var recipe = await db.Recipes
                .Include(r => r.Versions)
                    .ThenInclude(v => v.Details)
                .FirstOrDefaultAsync(r => r.VariantId == variant.VariantId);

            if (recipe == null)
            {
                recipe = new Recipe
                {
                    VariantId = variant.VariantId,
                    Description = $"Công thức cho {_product.ProductName} ({variant.Size})"
                };
                db.Recipes.Add(recipe);
                await db.SaveChangesAsync();
            }

            // Đánh dấu các version cũ là không current
            foreach (var v in recipe.Versions)
            {
                v.IsCurrent = false;
            }

            // Tạo version mới
            int newVerNum = recipe.Versions.Count + 1;
            var newVersion = new RecipeVersion
            {
                RecipeId = recipe.RecipeId,
                VersionNumber = newVerNum,
                EffectiveFrom = DateTime.UtcNow,
                IsCurrent = true,
                Note = $"Cập nhật định mức ngày {DateTime.Now:dd/MM/yyyy HH:mm}"
            };

            foreach (var itm in _currentItems)
            {
                newVersion.Details.Add(new RecipeDetail
                {
                    IngredientId = itm.IngredientId,
                    Quantity = itm.Quantity,
                    Uom = itm.Uom
                });
            }

            db.RecipeVersions.Add(newVersion);
            await db.SaveChangesAsync();

            MessageBox.Show($"🎉 Đã lưu công thức pha chế thành công cho {_product.ProductName} ({SelectedSize})!", 
                "Lưu Công Thức Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi lưu công thức: {ex.Message}", "Lỗi CSDL", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
