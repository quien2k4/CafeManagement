using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.Inventory;

public partial class ImportStockWindow : Window
{
    public List<Ingredient> AvailableIngredients { get; }
    public Ingredient? SelectedIngredient { get; private set; }
    public decimal ImportQuantity { get; private set; }
    public string Note { get; private set; } = string.Empty;

    private decimal _evaluatedQuantity = 0;
    private bool _isFormulaValid = false;

    public ImportStockWindow(List<Ingredient> ingredients)
    {
        InitializeComponent();
        AvailableIngredients = ingredients;
        DataContext = this;

        if (ingredients.Count > 0)
        {
            CboIngredients.SelectedIndex = 0;
        }

        TxtQuantity.Text = "1000";
    }

    private void CboIngredients_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboIngredients.SelectedItem is Ingredient ing)
        {
            TxtUom.Text = ing.BaseUom;
            UpdatePreview();
        }
    }

    private void TxtQuantity_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    private void Multiply_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string multiplier)
        {
            string current = TxtQuantity.Text.Trim();
            if (string.IsNullOrEmpty(current))
            {
                TxtQuantity.Text = multiplier;
            }
            else
            {
                TxtQuantity.Text = $"{current} * {multiplier}";
            }
            TxtQuantity.CaretIndex = TxtQuantity.Text.Length;
            TxtQuantity.Focus();
        }
    }

    private void UpdatePreview()
    {
        if (TxtPreviewResult == null || PnlPreview == null) return;

        string input = TxtQuantity.Text;
        string uom = (CboIngredients.SelectedItem as Ingredient)?.BaseUom ?? "g";

        var (success, val, explanation) = EvaluateMathExpression(input);

        if (success && val > 0)
        {
            _evaluatedQuantity = val;
            _isFormulaValid = true;

            TxtPreviewResult.Text = $"{val:N1} {uom}";
            TxtPreviewResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E8449"));
            PnlPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAFAF1"));
            PnlPreview.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4EFDF"));
        }
        else
        {
            _evaluatedQuantity = 0;
            _isFormulaValid = false;

            TxtPreviewResult.Text = "⚠️ Biểu thức không hợp lệ (VD: 330 * 24)";
            TxtPreviewResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0392B"));
            PnlPreview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEDEC"));
            PnlPreview.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FADBD8"));
        }
    }

    private static (bool Success, decimal Result, string Explanation) EvaluateMathExpression(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (false, 0, "");

        try
        {
            string cleaned = input.Trim()
                .Replace('x', '*')
                .Replace('X', '*')
                .Replace(" ", "");

            var dt = new DataTable();
            var computed = dt.Compute(cleaned, "");
            if (computed != null && decimal.TryParse(computed.ToString(), out decimal val))
            {
                return (true, val, cleaned);
            }
        }
        catch
        {
        }

        return (false, 0, "");
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (CboIngredients.SelectedItem is not Ingredient ing)
        {
            MessageBox.Show("Vui lòng chọn nguyên liệu cần nhập kho!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        UpdatePreview();

        if (!_isFormulaValid || _evaluatedQuantity <= 0)
        {
            MessageBox.Show("Số lượng nhập hoặc biểu thức chưa hợp lệ!\nVí dụ: 330 * 24 hoặc 5000", "Lỗi nhập liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtQuantity.Focus();
            return;
        }

        string rawInput = TxtQuantity.Text.Trim();
        string userNote = string.IsNullOrWhiteSpace(TxtNote.Text) ? "Nhập hàng từ NCC" : TxtNote.Text.Trim();
        string formulaDisplay = (rawInput.Contains('*') || rawInput.Contains('x') || rawInput.Contains('X') || rawInput.Contains('+'))
            ? $"{rawInput} = {_evaluatedQuantity:N1} {ing.BaseUom}"
            : "";

        // Mở cửa sổ xác nhận với tên món và số lượng được in đậm rõ ràng
        var confirmDialog = new ConfirmImportWindow(ing.IngredientName, _evaluatedQuantity, ing.BaseUom, formulaDisplay, userNote)
        {
            Owner = this
        };

        if (confirmDialog.ShowDialog() != true)
        {
            // Người dùng chọn "Quay lại sửa"
            return;
        }

        SelectedIngredient = ing;
        ImportQuantity = _evaluatedQuantity;

        if (!string.IsNullOrEmpty(formulaDisplay))
        {
            Note = $"Quy cách [{formulaDisplay}] - {userNote}";
        }
        else
        {
            Note = userNote;
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
