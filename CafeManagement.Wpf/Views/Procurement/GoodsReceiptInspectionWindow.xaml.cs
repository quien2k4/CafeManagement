using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Wpf.Views.Procurement;

public class InspectionItemRow : INotifyPropertyChanged
{
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public string Uom { get; set; } = "g";
    public decimal UnitPrice { get; set; }

    private decimal _deliveredQuantity;
    public decimal DeliveredQuantity
    {
        get => _deliveredQuantity;
        set { SetField(ref _deliveredQuantity, value); OnPropertyChanged(nameof(TotalAcceptedPrice)); }
    }

    private decimal _acceptedQuantity;
    public decimal AcceptedQuantity
    {
        get => _acceptedQuantity;
        set { SetField(ref _acceptedQuantity, value); OnPropertyChanged(nameof(TotalAcceptedPrice)); }
    }

    private decimal _quarantinedQuantity;
    public decimal QuarantinedQuantity
    {
        get => _quarantinedQuantity;
        set => SetField(ref _quarantinedQuantity, value);
    }

    private decimal _rejectedQuantity;
    public decimal RejectedQuantity
    {
        get => _rejectedQuantity;
        set => SetField(ref _rejectedQuantity, value);
    }

    private string? _rejectionReason;
    public string? RejectionReason
    {
        get => _rejectionReason;
        set => SetField(ref _rejectionReason, value);
    }

    public decimal TotalAcceptedPrice => AcceptedQuantity * UnitPrice;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public partial class GoodsReceiptInspectionWindow : Window
{
    public CreateGoodsReceiptRequest RequestData { get; private set; } = new();

    private readonly PurchaseOrderDto _po;
    private readonly UserSession _session;
    private readonly ObservableCollection<InspectionItemRow> _items = [];

    public GoodsReceiptInspectionWindow(PurchaseOrderDto po, UserSession session)
    {
        InitializeComponent();

        _po = po;
        _session = session;

        TxtPoCode.Text = _po.PoCode;
        TxtSupplierName.Text = _po.SupplierName;
        TxtBranchName.Text = _po.BranchName;
        TxtReceiverName.Text = _session.FullName;

        // Khởi tạo các dòng mặt hàng từ PO
        foreach (var d in _po.Details)
        {
            decimal remaining = Math.Max(0, d.OrderedQuantity - d.ReceivedQuantity);
            decimal initialDelivery = remaining > 0 ? remaining : d.OrderedQuantity;

            _items.Add(new InspectionItemRow
            {
                IngredientId = d.IngredientId,
                IngredientCode = d.IngredientCode,
                IngredientName = d.IngredientName,
                OrderedQuantity = d.OrderedQuantity,
                Uom = d.Uom,
                UnitPrice = d.UnitPrice,
                DeliveredQuantity = initialDelivery,
                AcceptedQuantity = initialDelivery, // Mặc định nhận đủ đạt chuẩn
                QuarantinedQuantity = 0,
                RejectedQuantity = 0,
                RejectionReason = null
            });
        }

        DgInspectionItems.ItemsSource = _items;
        UpdateSummary();
    }

    private void ItemQty_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (TxtTotalAcceptedCount == null || TxtTotalRejectedCount == null || TxtTotalAcceptedAmountDisplay == null)
            return;

        decimal totalAccepted = _items.Sum(i => i.AcceptedQuantity);
        decimal totalRejected = _items.Sum(i => i.RejectedQuantity);
        decimal totalAmount = _items.Sum(i => i.TotalAcceptedPrice);

        TxtTotalAcceptedCount.Text = $"{totalAccepted:N1} đvt";
        TxtTotalRejectedCount.Text = $"{totalRejected:N1} đvt";
        TxtTotalAcceptedAmountDisplay.Text = $"{totalAmount:N0} đ";
    }

    private void ConfirmReceiptButton_Click(object sender, RoutedEventArgs e)
    {
        string deliveryPerson = TxtDeliveryPerson.Text.Trim();
        if (string.IsNullOrWhiteSpace(deliveryPerson))
        {
            MessageBox.Show("Vui lòng nhập tên người giao hàng / tài xế.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDeliveryPerson.Focus();
            return;
        }

        // Kiểm tra xem có mặt hàng nào bị từ chối mà chưa ghi lý do không
        var missingReason = _items.FirstOrDefault(i => i.RejectedQuantity > 0 && string.IsNullOrWhiteSpace(i.RejectionReason));
        if (missingReason != null)
        {
            var res = MessageBox.Show(
                $"Mặt hàng '{missingReason.IngredientName}' có {missingReason.RejectedQuantity} {missingReason.Uom} bị từ chối trả lại nhưng chưa ghi rõ lý do.\nBạn có muốn tự động ghi lý do 'Hàng không đạt chất lượng/hư hỏng' và tiếp tục?",
                "Thiếu lý do từ chối", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            foreach (var item in _items.Where(i => i.RejectedQuantity > 0 && string.IsNullOrWhiteSpace(i.RejectionReason)))
            {
                item.RejectionReason = "Hàng không đạt chất lượng/hư hỏng bao bì";
            }
        }

        RequestData = new CreateGoodsReceiptRequest
        {
            PoId = _po.PoId,
            BranchId = _po.BranchId,
            ReceivedByUserId = _session.UserId,
            DeliveryPerson = deliveryPerson,
            InvoiceNumber = TxtInvoiceNumber.Text.Trim(),
            Note = TxtReceiptNote.Text.Trim(),
            Items = _items.Select(i => new CreateGoodsReceiptItemRequest
            {
                IngredientId = i.IngredientId,
                DeliveredQuantity = i.DeliveredQuantity,
                AcceptedQuantity = i.AcceptedQuantity,
                QuarantinedQuantity = i.QuarantinedQuantity,
                RejectedQuantity = i.RejectedQuantity,
                RejectionReason = i.RejectionReason,
                UnitPrice = i.UnitPrice,
                Uom = i.Uom
            }).ToList()
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
