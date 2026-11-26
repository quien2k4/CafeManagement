using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Branches;

public partial class BranchManagementWindow : Window
{
    private List<BranchItemViewModel> _allBranches = [];

    public BranchManagementWindow()
    {
        InitializeComponent();
        Loaded += BranchManagementWindow_Loaded;
    }

    private async void BranchManagementWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadBranchesAsync();
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            var branches = await BranchApiClient.Instance.GetBranchesAsync(activeOnly: false);
            _allBranches = branches.Select(b => new BranchItemViewModel
            {
                BranchId = b.BranchId,
                BranchCode = b.BranchCode,
                BranchName = b.BranchName,
                Address = b.Address,
                Phone = b.Phone,
                IsActive = b.IsActive
            }).OrderBy(b => b.BranchId).ToList();

            int activeCount = _allBranches.Count(b => b.IsActive);
            TxtSummary.Text = $"Tổng số: {_allBranches.Count} chi nhánh | Đang hoạt động: {activeCount}";

            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh sách chi nhánh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        string kw = TxtSearch?.Text?.Trim().ToLower() ?? "";
        var filtered = string.IsNullOrEmpty(kw)
            ? _allBranches
            : _allBranches.Where(b => b.BranchCode.ToLower().Contains(kw) ||
                                     b.BranchName.ToLower().Contains(kw) ||
                                     b.Address.ToLower().Contains(kw) ||
                                     b.Phone.ToLower().Contains(kw)).ToList();

        DgBranches.ItemsSource = new ObservableCollection<BranchItemViewModel>(filtered);
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadBranchesAsync();
    }

    private async void BtnAddNewBranch_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new BranchEditWindow(null);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            await LoadBranchesAsync();
        }
    }

    private async void BtnEditRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BranchItemViewModel item)
        {
            var dto = new BranchDto
            {
                BranchId = item.BranchId,
                BranchCode = item.BranchCode,
                BranchName = item.BranchName,
                Address = item.Address,
                Phone = item.Phone,
                IsActive = item.IsActive
            };

            var dlg = new BranchEditWindow(dto);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                await LoadBranchesAsync();
            }
        }
    }

    private async void BtnToggleRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is BranchItemViewModel item)
        {
            string actionText = item.IsActive ? "TẠM NGƯNG HOẠT ĐỘNG" : "KÍCH HOẠT LẠI";
            var res = MessageBox.Show($"Bạn có chắc chắn muốn {actionText} chi nhánh '{item.BranchName}' ({item.BranchCode}) không?", 
                "Xác Nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                bool ok = await BranchApiClient.Instance.ToggleBranchActiveAsync(item.BranchId);
                if (ok)
                {
                    await LoadBranchesAsync();
                }
                else
                {
                    MessageBox.Show("Không thể thay đổi trạng thái chi nhánh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async void DgBranches_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgBranches.SelectedItem is BranchItemViewModel item)
        {
            var dto = new BranchDto
            {
                BranchId = item.BranchId,
                BranchCode = item.BranchCode,
                BranchName = item.BranchName,
                Address = item.Address,
                Phone = item.Phone,
                IsActive = item.IsActive
            };

            var dlg = new BranchEditWindow(dto);
            dlg.Owner = this;
            if (dlg.ShowDialog() == true)
            {
                await LoadBranchesAsync();
            }
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class BranchItemViewModel
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public string StatusText => IsActive ? "🟢 Đang hoạt động" : "🔴 Tạm ngưng";
    public string StatusColor => IsActive ? "#15803D" : "#B91C1C";
    public string StatusBackground => IsActive ? "#DCFCE7" : "#FEE2E2";

    public string ToggleButtonText => IsActive ? "🔒 Tạm ngưng" : "🔓 Mở lại";
    public string ToggleButtonForeground => IsActive ? "#B91C1C" : "#15803D";
    public string ToggleButtonBackground => IsActive ? "#FEE2E2" : "#DCFCE7";
}
