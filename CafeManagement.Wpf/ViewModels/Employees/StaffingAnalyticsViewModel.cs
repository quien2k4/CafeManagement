using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Core.DTOs.Reports;
using CafeManagement.Core.Utils;
using CafeManagement.Wpf.Services.Authentication;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.ViewModels.Employees;

public partial class StaffingAnalyticsViewModel : ObservableObject
{
    private readonly ReportApiClient _reportApi;
    private readonly AuthenticationApiClient _authApi;

    public UserSession? CurrentSession { get; }

    [ObservableProperty]
    private ObservableCollection<BranchDto> _branches = [];

    [ObservableProperty]
    private BranchDto? _selectedBranch;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDays7))]
    [NotifyPropertyChangedFor(nameof(IsDays14))]
    [NotifyPropertyChangedFor(nameof(IsDays30))]
    private int _selectedDays = 14;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDayTypeAll))]
    [NotifyPropertyChangedFor(nameof(IsDayTypeWeekday))]
    [NotifyPropertyChangedFor(nameof(IsDayTypeWeekend))]
    private string _selectedDayType = "ALL"; // ALL, WEEKDAY, WEEKEND

    public bool IsDays7 => SelectedDays == 7;
    public bool IsDays14 => SelectedDays == 14;
    public bool IsDays30 => SelectedDays == 30;

    public bool IsDayTypeAll => SelectedDayType == "ALL";
    public bool IsDayTypeWeekday => SelectedDayType == "WEEKDAY";
    public bool IsDayTypeWeekend => SelectedDayType == "WEEKEND";

    [ObservableProperty]
    private DateTime _selectedCalendarDate = DateTime.Today;

    [ObservableProperty]
    private bool _isHolidaySurge;

    [ObservableProperty]
    private string _holidayName = string.Empty;

    [ObservableProperty]
    private string _holidayDescription = string.Empty;

    [ObservableProperty]
    private string _holidayBadgeColor = "#E53E3E";

    [ObservableProperty]
    private BranchStaffingAnalyticsResponse? _analyticsData;

    [ObservableProperty]
    private ObservableCollection<HourlyStaffingTrafficDto> _hourlyTraffics = [];

    [ObservableProperty]
    private ObservableCollection<StaffingRecommendationItemDto> _recommendations = [];

    [ObservableProperty]
    private ObservableCollection<HolidayInfo> _upcomingHolidays = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public StaffingAnalyticsViewModel(UserSession? session = null)
    {
        CurrentSession = session;
        _reportApi = ReportApiClient.Instance;
        _authApi = AuthenticationApiClient.Instance;

        CheckSelectedDateHoliday();
    }

    public async Task InitializeAsync(int? defaultBranchId = null)
    {
        IsLoading = true;
        StatusMessage = "Đang tải danh sách chi nhánh và phân tích lưu lượng...";
        try
        {
            var branchList = (await _authApi.GetBranchesAsync()).Where(b => b.IsActive).ToList();
            Branches.Clear();

            // Thêm tùy chọn "Tất cả chi nhánh"
            var allBranch = new BranchDto { BranchId = 0, BranchName = "Tất cả chi nhánh", BranchCode = "ALL" };
            Branches.Add(allBranch);

            foreach (var b in branchList)
            {
                Branches.Add(b);
            }

            if (defaultBranchId.HasValue && defaultBranchId.Value > 0)
            {
                SelectedBranch = Branches.FirstOrDefault(b => b.BranchId == defaultBranchId.Value) ?? Branches.FirstOrDefault();
            }
            else
            {
                SelectedBranch = Branches.FirstOrDefault();
            }

            await LoadAnalyticsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi khởi tạo: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadAnalyticsAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang phân tích lưu lượng đơn hàng & tạo khuyến nghị nhân sự...";
        try
        {
            int? branchId = (SelectedBranch != null && SelectedBranch.BranchId > 0) ? SelectedBranch.BranchId : null;

            var result = await _reportApi.GetStaffingAnalyticsAsync(
                branchId: branchId,
                days: SelectedDays,
                dayType: SelectedDayType);

            if (result != null)
            {
                AnalyticsData = result;

                HourlyTraffics.Clear();
                foreach (var h in result.HourlyTraffics)
                {
                    HourlyTraffics.Add(h);
                }

                Recommendations.Clear();
                foreach (var rec in result.Recommendations)
                {
                    Recommendations.Add(rec);
                }

                UpcomingHolidays.Clear();
                foreach (var hol in result.UpcomingHolidays)
                {
                    UpcomingHolidays.Add(hol);
                }

                StatusMessage = $"Đã phân tích {result.TotalOrdersAnalyzed:N0} đơn hàng trong {result.DaysAnalyzed} ngày.";
            }
            else
            {
                StatusMessage = "Không có dữ liệu phân tích phù hợp.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Lỗi phân tích: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void CheckSelectedDateHoliday()
    {
        var hol = HolidayCalendarHelper.CheckHoliday(SelectedCalendarDate);
        if (hol != null)
        {
            IsHolidaySurge = true;
            HolidayName = hol.HolidayName;
            HolidayDescription = $"{hol.Description} (Dự kiến lưu lượng tăng x{hol.ExpectedSurgeMultiplier:0.0})";
            HolidayBadgeColor = hol.HighlightColor;
        }
        else
        {
            IsHolidaySurge = false;
            HolidayName = string.Empty;
            HolidayDescription = string.Empty;
            HolidayBadgeColor = "#718096";
        }
    }

    [RelayCommand]
    public async Task FilterDaysAsync(object? parameter)
    {
        if (parameter != null && int.TryParse(parameter.ToString(), out int days))
        {
            SelectedDays = days;
            await LoadAnalyticsAsync();
        }
    }

    [RelayCommand]
    public async Task FilterDayTypeAsync(object? parameter)
    {
        if (parameter != null)
        {
            SelectedDayType = parameter.ToString() ?? "ALL";
            await LoadAnalyticsAsync();
        }
    }
}
