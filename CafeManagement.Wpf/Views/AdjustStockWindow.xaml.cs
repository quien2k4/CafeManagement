using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CafeManagement.Wpf.ViewModels;

namespace CafeManagement.Wpf.Views;

public partial class AdjustStockWindow : Window
{
    private readonly InventoryItemViewModel _item;
    public decimal NewQuantity { get; private set; }
    public decimal Difference => NewQuantity - _item.QuantityOnHand;
    public string Reason { get; private set; } = string.Empty;
    public string Note { get; private set; } = string.Empty;

    public AdjustStockWindow(InventoryItemViewModel item)
    {
        InitializeComponent();
        _item = item;

        TxtIngredientName.Text = item.IngredientName;
        TxtIngredientCode.Text = $"Mã: {item.IngredientCode}";
        TxtCurrentStock.Text = $"{item.QuantityOnHand:N1} {item.BaseUom}";
        TxtUom.Text = item.BaseUom;
        TxtNewQuantity.Text = item.QuantityOnHand.ToString("N1");
        NewQuantity = item.QuantityOnHand;

        UpdateDifferenceDisplay();
    }

    private void TxtNewQuantity_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateDifferenceDisplay();
    }

    private void UpdateDifferenceDisplay()
    {
        string raw = TxtNewQuantity.Text.Replace(",", ".").Trim();
        if (decimal.TryParse(raw, out decimal newQty) && newQty >= 0)
        {
            NewQuantity = newQty;
            decimal diff = NewQuantity - _item.QuantityOnHand;

            if (diff > 0)
            {
                TxtDifference.Text = $"+{diff:N1} {_item.BaseUom} (Tăng tồn kho)";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27AE60"));
                BrdDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAFAF1"));
            }
            else if (diff < 0)
            {
                TxtDifference.Text = $"{diff:N1} {_item.BaseUom} (Hao hụt / Giảm tồn kho)";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                BrdDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEDEC"));
            }
            else
            {
                TxtDifference.Text = $"0.0 {_item.BaseUom} (Không thay đổi)";
                TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7F8C8D"));
                BrdDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F3F5"));
            }
        }
        else
        {
            TxtDifference.Text = "Số lượng không hợp lệ";
            TxtDifference.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
            BrdDiff.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDEDEC"));
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        string raw = TxtNewQuantity.Text.Replace(",", ".").Trim();
        if (!decimal.TryParse(raw, out decimal newQty) || newQty < 0)
        {
            MessageBox.Show("Vui lòng nhập số lượng tồn kho thực tế hợp lệ (>= 0)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NewQuantity = newQty;

        string selectedReason = (CboReason.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Điều chỉnh kiểm kê";
        Reason = selectedReason;
        Note = TxtNote.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}