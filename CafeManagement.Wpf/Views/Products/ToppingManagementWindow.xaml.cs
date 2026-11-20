using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Toppings;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Products;

public partial class ToppingManagementWindow : Window
{
    private List<Ingredient> _ingredients = [];
    private ToppingDto? _selectedTopping;
    private readonly ObservableCollection<ToppingRecipeDetailDto> _currentBOM = [];

    public ToppingManagementWindow()
    {
        InitializeComponent();
        LstBOMItems.ItemsSource = _currentBOM;
        Loaded += ToppingManagementWindow_Loaded;
    }

    private async void ToppingManagementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadIngredientsAsync();
        await LoadToppingsAsync();
    }

    private async Task LoadIngredientsAsync()
    {
        try
        {
            _ingredients = await InventoryApiClient.Instance.GetIngredientsAsync(activeOnly: true);
            CboIngredient.ItemsSource = _ingredients;
            if (_ingredients.Count > 0)
            {
                CboIngredient.SelectedIndex = 0;
                TxtIngredientUom.Text = _ingredients[0].BaseUom;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh sách nguyên liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        CboIngredient.SelectionChanged += (s, ev) =>
        {
            if (CboIngredient.SelectedItem is Ingredient ing)
            {
                TxtIngredientUom.Text = ing.BaseUom;
            }
        };
    }

    private async Task LoadToppingsAsync(int? selectToppingId = null)
    {
        try
        {
            var list = await ProductApiClient.Instance.GetToppingDtosAsync(activeOnly: false);
            LstToppings.ItemsSource = list;
            TxtToppingCount.Text = $"({list.Count} topping)";

            if (list.Count > 0)
            {
                if (selectToppingId.HasValue)
                {
                    var found = list.FirstOrDefault(t => t.ToppingId == selectToppingId.Value);
                    LstToppings.SelectedItem = found ?? list[0];
                }
                else
                {
                    LstToppings.SelectedIndex = 0;
                }
            }
            else
            {
                ResetFormForNewTopping();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh mục Topping: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LstToppings_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstToppings.SelectedItem is ToppingDto topping)
        {
            _selectedTopping = topping;
            TxtFormTitle.Text = $"⚙️ CẬP NHẬT TOPPING: {topping.ToppingName.ToUpper()}";
            TxtToppingCode.Text = topping.ToppingCode;
            TxtToppingCode.IsEnabled = false; // Mã topping không sửa khi edit
            TxtToppingName.Text = topping.ToppingName;
            TxtPrice.Text = topping.Price.ToString("N0");
            ChkToppingActive.IsChecked = topping.IsActive;

            _currentBOM.Clear();
            foreach (var detail in topping.RecipeDetails)
            {
                _currentBOM.Add(detail);
            }
            TxtBOMCount.Text = $"({_currentBOM.Count} nguyên liệu)";
        }
    }

    private void CreateNewTopping_Click(object sender, RoutedEventArgs e)
    {
        LstToppings.SelectedItem = null;
        ResetFormForNewTopping();
    }

    private void ResetFormForNewTopping()
    {
        _selectedTopping = null;
        TxtFormTitle.Text = "⚙️ THIẾT LẬP TOPPING MỚI";
        TxtToppingCode.IsEnabled = true;
        TxtToppingCode.Text = "TOP_" + DateTime.Now.ToString("HHmmss");
        TxtToppingName.Text = string.Empty;
        TxtPrice.Text = "5000";
        ChkToppingActive.IsChecked = true;

        _currentBOM.Clear();
        TxtBOMCount.Text = "(0 nguyên liệu)";
        TxtToppingName.Focus();
    }

    private void AddBOMItem_Click(object sender, RoutedEventArgs e)
    {
        if (CboIngredient.SelectedItem is not Ingredient ing)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtIngredientQuantity.Text.Trim(), out decimal qty) || qty <= 0)
        {
            MessageBox.Show("Định lượng nguyên liệu phải lớn hơn 0!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = _currentBOM.FirstOrDefault(b => b.IngredientId == ing.IngredientId);
        if (existing != null)
        {
            existing.Quantity = qty;
            existing.Uom = ing.BaseUom;
            // Force refresh list
            int idx = _currentBOM.IndexOf(existing);
            _currentBOM[idx] = existing;
        }
        else
        {
            _currentBOM.Add(new ToppingRecipeDetailDto
            {
                IngredientId = ing.IngredientId,
                IngredientCode = ing.IngredientCode,
                IngredientName = ing.IngredientName,
                Quantity = qty,
                Uom = ing.BaseUom
            });
        }

        TxtBOMCount.Text = $"({_currentBOM.Count} nguyên liệu)";
        TxtIngredientQuantity.Text = "30";
    }

    private void DeleteBOMItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ToppingRecipeDetailDto item)
        {
            _currentBOM.Remove(item);
            TxtBOMCount.Text = $"({_currentBOM.Count} nguyên liệu)";
        }
    }

    private async void SaveTopping_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtToppingCode.Text.Trim().ToUpper();
        string name = TxtToppingName.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("Vui lòng nhập mã Topping!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lòng nhập tên Topping!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string rawPrice = TxtPrice.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawPrice, out decimal price) || price < 0)
        {
            MessageBox.Show("Giá phụ thu Topping không hợp lệ!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool isActive = ChkToppingActive.IsChecked == true;

        var recipeDetails = _currentBOM.Select(b => new ToppingRecipeDetailItem
        {
            IngredientId = b.IngredientId,
            Quantity = b.Quantity,
            Uom = b.Uom
        }).ToList();

        BtnSaveTopping.IsEnabled = false;
        BtnSaveTopping.Content = "Đang lưu...";

        try
        {
            if (_selectedTopping == null)
            {
                var req = new ToppingCreateRequest
                {
                    ToppingCode = code,
                    ToppingName = name,
                    Price = price,
                    IsActive = isActive,
                    RecipeDetails = recipeDetails
                };

                var res = await ProductApiClient.Instance.CreateToppingAsync(req);
                if (res != null)
                {
                    MessageBox.Show($"Tạo Topping '{res.ToppingName}' kèm công thức thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadToppingsAsync(res.ToppingId);
                }
            }
            else
            {
                var req = new ToppingUpdateRequest
                {
                    ToppingName = name,
                    Price = price,
                    IsActive = isActive,
                    RecipeDetails = recipeDetails
                };

                var res = await ProductApiClient.Instance.UpdateToppingAsync(_selectedTopping.ToppingId, req);
                if (res != null)
                {
                    MessageBox.Show($"Cập nhật Topping '{res.ToppingName}' & công thức thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadToppingsAsync(res.ToppingId);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi lưu Topping: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSaveTopping.IsEnabled = true;
            BtnSaveTopping.Content = "💾 Lưu Topping & Công Thức";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
