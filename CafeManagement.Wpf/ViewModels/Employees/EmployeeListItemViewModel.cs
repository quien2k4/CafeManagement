namespace CafeManagement.Wpf.ViewModels.Employees;

public class EmployeeListItemViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime HireDate { get; set; }

    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;

    public string StatusBadge => Status == "ACTIVE" ? "🟢 Đang làm việc" : "⏸ Đã nghỉ / Tạm ngưng";
    public string StatusColor => Status == "ACTIVE" ? "#27AE60" : "#E74C3C";

    public string RoleBadgeColor => RoleCode switch
    {
        "OWNER" => "#8E44AD",
        "BRANCH_MANAGER" => "#2980B9",
        "CASHIER" => "#27AE60",
        "BARISTA" => "#D35400",
        "INVENTORY_STAFF" => "#16A085",
        _ => "#7F8C8D"
    };
}
