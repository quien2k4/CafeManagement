using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CafeManagement.Core.DTOs.Inventory;

namespace CafeManagement.Wpf.Views.Inventory;

public class ReconcileItemViewModel : INotifyPropertyChanged
{
    public int DetailId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string Uom { get; set; } = "g";
    public decimal DispatchedQuantity { get; set; }

    private decimal _receivedQuantity;
    public decimal ReceivedQuantity
    {
        get => _receivedQuantity;
        set
        {
            if (_receivedQuantity != value)
            {
                _receivedQuantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscrepancyQuantity));
            }
        }
    }

    public decimal DiscrepancyQuantity => ReceivedQuantity - DispatchedQuantity;

    private string? _discrepancyReason;
    public string? DiscrepancyReason
    {
        get => _discrepancyReason;
        set
        {
            if (_discrepancyReason != value)
            {
                _discrepancyReason = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}

public partial class ReconcileTransferWindow : Window
{
    public StockTransferDto Transfer { get; }
    public ObservableCollection<ReconcileItemViewModel> Items { get; } = [];
    public ReceiveAndReconcileTransferRequest? ResultRequest { get; private set; }

    public ReconcileTransferWindow(StockTransferDto transfer)
    {
        InitializeComponent();
        Transfer = transfer;

        TxtTransferCode.Text = transfer.TransferCode;
        TxtRoute.Text = $"Từ: {transfer.FromBranchName} ➔ Đến: {transfer.ToBranchName}";

        foreach (var d in transfer.Details)
        {
            Items.Add(new ReconcileItemViewModel
            {
                DetailId = d.DetailId,
                IngredientName = d.IngredientName,
                Uom = d.Uom,
                DispatchedQuantity = d.DispatchedQuantity,
                ReceivedQuantity = d.DispatchedQuantity, // Mặc định nhận đủ
                DiscrepancyReason = string.Empty
            });
        }

        GridItems.ItemsSource = Items;
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        // Kiểm tra xem có món nào có chênh lệch âm mà chưa nhập lý do không
        var missingReasons = Items.Where(i => i.DiscrepancyQuantity != 0 && string.IsNullOrWhiteSpace(i.DiscrepancyReason)).ToList();
        if (missingReasons.Count > 0)
        {
            string names = string.Join(", ", missingReasons.Select(m => m.IngredientName));
            var ans = MessageBox.Show(
                $"Có {missingReasons.Count} nguyên liệu có sai lệch ({names}) nhưng chưa ghi rõ lý do.\nBạn có muốn tiếp tục lưu mà không cần lý do chi tiết?",
                "Cảnh Báo Chênh Lệch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (ans != MessageBoxResult.Yes) return;
        }

        ResultRequest = new ReceiveAndReconcileTransferRequest
        {
            TransferId = Transfer.TransferId,
            ReconciliationNote = TxtReconciliationNote.Text.Trim(),
            Items = Items.Select(i => new ReconcileTransferItemRequest
            {
                DetailId = i.DetailId,
                ReceivedQuantity = i.ReceivedQuantity,
                DiscrepancyReason = i.DiscrepancyReason
            }).ToList()
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
