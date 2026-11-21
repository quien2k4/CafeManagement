using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Inventory;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.ViewModels;

public partial class StockTransferManagementViewModel : ObservableObject
{
    public UserSession CurrentSession { get; }

    [ObservableProperty]
    private ObservableCollection<StockTransferDto> _transfers = [];

    private List<StockTransferDto> _allTransfers = [];

    [ObservableProperty]
    private StockTransferDto? _selectedTransfer;

    [ObservableProperty]
    private string _filterStatus = "ALL";

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _requestedCount;

    [ObservableProperty]
    private int _inTransitCount;

    [ObservableProperty]
    private int _completedCount;

    public bool CanApproveOrDispatch => SelectedTransfer != null &&
        (SelectedTransfer.FromBranchId == CurrentSession.BranchId || CurrentSession.IsOwnerOrAdmin) &&
        (SelectedTransfer.Status == "REQUESTED" || SelectedTransfer.Status == "APPROVED");

    public bool CanReceiveAndReconcile => SelectedTransfer != null &&
        (SelectedTransfer.ToBranchId == CurrentSession.BranchId || CurrentSession.IsOwnerOrAdmin) &&
        SelectedTransfer.Status == "IN_TRANSIT";

    public bool CanCancel => SelectedTransfer != null &&
        SelectedTransfer.Status != "COMPLETED" &&
        SelectedTransfer.Status != "COMPLETED_DISCREPANCY" &&
        SelectedTransfer.Status != "CANCELLED" &&
        (SelectedTransfer.FromBranchId == CurrentSession.BranchId || SelectedTransfer.ToBranchId == CurrentSession.BranchId || CurrentSession.IsOwnerOrAdmin);

    public StockTransferManagementViewModel(UserSession session)
    {
        CurrentSession = session;
    }

    [RelayCommand]
    public async Task LoadTransfersAsync()
    {
        IsLoading = true;
        try
        {
            // Nếu là admin hoặc chủ quán thì xem toàn chuỗi, nếu là nhân viên chi nhánh thì xem chi nhánh hiện tại
            int? branchId = CurrentSession.IsOwnerOrAdmin ? null : CurrentSession.BranchId;
            _allTransfers = await StockTransferApiClient.Instance.GetTransfersAsync(branchId, "ALL");

            RequestedCount = _allTransfers.Count(t => t.Status == "REQUESTED");
            InTransitCount = _allTransfers.Count(t => t.Status == "IN_TRANSIT");
            CompletedCount = _allTransfers.Count(t => t.Status == "COMPLETED" || t.Status == "COMPLETED_DISCREPANCY");

            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh sách điều chuyển: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void FilterByStatus(string status)
    {
        FilterStatus = status;
        ApplyFilter();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedTransferChanged(StockTransferDto? value)
    {
        OnPropertyChanged(nameof(CanApproveOrDispatch));
        OnPropertyChanged(nameof(CanReceiveAndReconcile));
        OnPropertyChanged(nameof(CanCancel));
    }

    private void ApplyFilter()
    {
        var list = _allTransfers.AsEnumerable();

        if (FilterStatus == "REQUESTED")
        {
            list = list.Where(t => t.Status == "REQUESTED");
        }
        else if (FilterStatus == "IN_TRANSIT")
        {
            list = list.Where(t => t.Status == "IN_TRANSIT");
        }
        else if (FilterStatus == "COMPLETED")
        {
            list = list.Where(t => t.Status == "COMPLETED" || t.Status == "COMPLETED_DISCREPANCY");
        }
        else if (FilterStatus == "NEED_RECEIVE")
        {
            list = list.Where(t => t.Status == "IN_TRANSIT" && (t.ToBranchId == CurrentSession.BranchId || CurrentSession.IsOwnerOrAdmin));
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            string kw = SearchKeyword.Trim().ToLower();
            list = list.Where(t =>
                t.TransferCode.ToLower().Contains(kw) ||
                t.FromBranchName.ToLower().Contains(kw) ||
                t.ToBranchName.ToLower().Contains(kw) ||
                (t.RequestNote?.ToLower().Contains(kw) == true) ||
                (t.ReconciliationNote?.ToLower().Contains(kw) == true));
        }

        Transfers = new ObservableCollection<StockTransferDto>(list.ToList());
        if (SelectedTransfer == null || !Transfers.Any(t => t.TransferId == SelectedTransfer.TransferId))
        {
            SelectedTransfer = Transfers.FirstOrDefault();
        }
    }
}
