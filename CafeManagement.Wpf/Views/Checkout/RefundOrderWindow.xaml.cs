using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Orders;
using CafeManagement.Core.DTOs.Payments;
using CafeManagement.Wpf.ViewModels.Reports;

namespace CafeManagement.Wpf.Views.Checkout;

public class RefundItemSelectionModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public long OrderItemId { get; set; }
    public int VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
    public string ToppingsSummary { get; set; } = string.Empty;

    public string DisplayTitle => $"{ProductName} (Size {Size})";
    public string DisplayDetail => $"SL: {Quantity} × {UnitPrice:N0} đ{(string.IsNullOrWhiteSpace(ToppingsSummary) ? "" : " • Topping: " + ToppingsSummary)}";
    public string DisplayLineTotal => $"{LineAmount:N0} đ";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class RefundOrderWindow : Window
{
    private readonly long _orderId;
    private readonly decimal _maxRefundable;
    private readonly ObservableCollection<RefundItemSelectionModel> _items = [];

    public RefundCreateRequest? ResultRequest { get; private set; }

    public RefundOrderWindow(OrderReportItemViewModel orderReport, OrderDto? fullOrder, int userId, string userName)
    {
        InitializeComponent();

        _orderId = orderReport.OrderId;
        _maxRefundable = orderReport.RemainingRefundableAmount;

        TxtOrderNumber.Text = $"#{orderReport.OrderNumber}";
        TxtOrderDate.Text = orderReport.CreatedAt.ToString("dd/MM/yyyy HH:mm");

        TxtFinalAmount.Text = $"{orderReport.FinalAmount:N0} đ";
        TxtRefundedAmount.Text = $"{orderReport.RefundedAmount:N0} đ";
        TxtMaxRefundable.Text = $"{_maxRefundable:N0} đ";

        // Khởi tạo danh sách món từ fullOrder
        if (fullOrder?.Items != null && fullOrder.Items.Count > 0)
        {
            foreach (var item in fullOrder.Items)
            {
                string topSummary = item.Toppings != null && item.Toppings.Count > 0
                    ? string.Join(", ", item.Toppings.Select(t => $"{t.Quantity}x {t.ToppingName}"))
                    : "";

                _items.Add(new RefundItemSelectionModel
                {
                    IsSelected = false,
                    OrderItemId = item.OrderItemId,
                    VariantId = item.VariantId,
                    ProductName = item.ProductName,
                    Size = item.Size,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineAmount = item.SubTotal,
                    ToppingsSummary = topSummary
                });
            }
        }

        ItemsListControl.ItemsSource = _items;
        TxtRefundAmount.Text = "0";
    }

    public RefundOrderWindow(OrderReportItemViewModel orderReport, int userId, string userName)
        : this(orderReport, null, userId, userName)
    {
    }

    private void RecalculateFromSelection()
    {
        var selected = _items.Where(i => i.IsSelected).ToList();
        if (selected.Count > 0)
        {
            decimal total = selected.Sum(i => i.LineAmount);
            if (total > _maxRefundable) total = _maxRefundable;
            TxtRefundAmount.Text = total.ToString("0.##");
        }
        else
        {
            TxtRefundAmount.Text = "0";
        }
    }

    private void OnItemSelectionChanged(object sender, RoutedEventArgs e)
    {
        RecalculateFromSelection();

        // Đồng bộ trạng thái checkbox Chọn tất cả
        bool allSelected = _items.Count > 0 && _items.All(i => i.IsSelected);
        ChkSelectAll.IsChecked = allSelected;
    }

    private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var item in _items)
        {
            item.IsSelected = true;
        }
        RecalculateFromSelection();
    }

    private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        // Chỉ uncheck nếu người dùng bấm uncheck (tránh vòng lặp khi OnItemSelectionChanged gán false)
        if (_items.All(i => i.IsSelected))
        {
            foreach (var item in _items)
            {
                item.IsSelected = false;
            }
            RecalculateFromSelection();
        }
    }

    private void BtnMaxAmount_Click(object sender, RoutedEventArgs e)
    {
        TxtRefundAmount.Text = _maxRefundable.ToString("0.##");
        foreach (var item in _items)
        {
            item.IsSelected = true;
        }
        ChkSelectAll.IsChecked = true;
    }

    private void CboReasonPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboReasonPreset.SelectedItem is ComboBoxItem item)
        {
            string text = item.Content?.ToString() ?? "";
            if (!text.StartsWith("--") && !text.StartsWith("Khác"))
            {
                TxtRefundReason.Text = text;
            }
            else if (text.StartsWith("Khác"))
            {
                TxtRefundReason.Text = "";
                TxtRefundReason.Focus();
            }
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        string amountText = TxtRefundAmount.Text.Trim().Replace(",", "").Replace(".", "");
        if (!decimal.TryParse(TxtRefundAmount.Text.Trim(), out decimal amount) || amount <= 0)
        {
            MessageBox.Show("Vui lòng nhập hoặc tích chọn món để có số tiền hoàn trả hợp lệ (> 0 đ)!", "Cảnh Báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (amount > _maxRefundable)
        {
            MessageBox.Show($"Số tiền hoàn trả ({amount:N0} đ) không được vượt quá số tiền tối đa có thể hoàn ({_maxRefundable:N0} đ)!", "Số Tiền Không Hợp Lệ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string reason = TxtRefundReason.Text.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            MessageBox.Show("Vui lòng nhập lý do hoàn tiền!", "Thiếu Thông Tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool restock = ChkRestockInventory.IsChecked == true;
        var selectedItems = _items.Where(i => i.IsSelected).ToList();

        if (restock && selectedItems.Count == 0 && _items.Count > 0)
        {
            var confirmRes = MessageBox.Show(
                "Bạn đã chọn 'Hoàn nhập nguyên liệu vào kho' nhưng chưa tích chọn món nào cụ thể!\n\n" +
                "• Bấm 'Yes' để hoàn nhập nguyên liệu cho TOÀN BỘ đơn hàng.\n" +
                "• Bấm 'No' để quay lại và tích chọn đúng các món cần hoàn nhập.",
                "Xác Nhận Phạm Vi Hoàn Kho",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmRes != MessageBoxResult.Yes)
            {
                return;
            }
        }

        string method = (CboRefundMethod.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "CASH";

        ResultRequest = new RefundCreateRequest
        {
            OrderId = _orderId,
            RefundAmount = amount,
            RefundMethod = method,
            RefundReason = reason,
            ReturnIngredientsToInventory = restock,
            Items = selectedItems.Select(i => new RefundItemRequest
            {
                OrderItemId = i.OrderItemId,
                VariantId = i.VariantId,
                ProductName = i.ProductName,
                Size = i.Size,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineAmount = i.LineAmount
            }).ToList()
        };

        DialogResult = true;
        Close();
    }
}