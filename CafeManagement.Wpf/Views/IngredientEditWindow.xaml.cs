using System.Windows;
using System.Windows.Controls;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class IngredientEditWindow : Window
{
    public Ingredient? IngredientToSave { get; private set; }
    public decimal MinStockAlert { get; private set; } = 500;
    public decimal InitialStock { get; private set; } = 0;
    public bool IsNew { get; }

    public IngredientEditWindow(Ingredient? existing = null, decimal currentMinStock = 500)
    {
        InitializeComponent();

        if (existing == null)
        {
            IsNew = true;
            TxtTitle.Text = "➕ THÊM NGUYÊN LIỆU MỚI VÀO KHO";
            TxtIngredientCode.Text = $"ING_{Random.Shared.Next(100, 999)}";
            PnlInitialStock.Visibility = Visibility.Visible;
            TxtMinStockAlert.Text = "500";
        }
        else
        {
            IsNew = false;
            IngredientToSave = existing;
            TxtTitle.Text = $"✏️ CHỈNH SỬA NGUYÊN LIỆU: {existing.IngredientName}";
            TxtIngredientCode.Text = existing.IngredientCode;
            TxtIngredientCode.IsEnabled = false; // Không sửa mã nguyên liệu
            TxtIngredientName.Text = existing.IngredientName;
            TxtMinStockAlert.Text = currentMinStock.ToString("N0");
            PnlInitialStock.Visibility = Visibility.Collapsed;

            // Set combobox Uom
            foreach (ComboBoxItem item in CboBaseUom.Items)
            {
                if (item.Content?.ToString()?.StartsWith(existing.BaseUom, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CboBaseUom.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtIngredientCode.Text.Trim();
        string name = TxtIngredientName.Text.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập đầy đủ mã và tên nguyên liệu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string rawUom = (CboBaseUom.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "g";
        // Lấy từ đầu tiên (ví dụ 'g (gram)' -> 'g')
        string uom = rawUom.Split(' ')[0];

        string rawMin = TxtMinStockAlert.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawMin, out decimal minStock) || minStock < 0)
        {
            MessageBox.Show("Mức cảnh báo an toàn không hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MinStockAlert = minStock;

        if (IsNew)
        {
            string rawInitial = TxtInitialStock.Text.Replace(",", ".").Trim();
            if (!decimal.TryParse(rawInitial, out decimal initialStock) || initialStock < 0)
            {
                MessageBox.Show("Số lượng tồn kho ban đầu không hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            InitialStock = initialStock;

            IngredientToSave = new Ingredient
            {
                IngredientCode = code,
                IngredientName = name,
                BaseUom = uom,
                IsActive = true
            };
        }
        else if (IngredientToSave != null)
        {
            IngredientToSave.IngredientName = name;
            IngredientToSave.BaseUom = uom;
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