namespace CafeManagement.Wpf.Models;

public class UserSession
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = "OWNER"; // "OWNER", "BRANCH_MANAGER", "CASHIER", "BARISTA", "INVENTORY_STAFF"
    public string RoleName { get; set; } = "Quản trị viên";
    public int BranchId { get; set; } = 1;
    public string BranchCode { get; set; } = "BR01";
    public string BranchName { get; set; } = "Chi nhánh Quận 1 (BR01)";

    public bool IsOwnerOrAdmin => RoleCode == "OWNER" || RoleCode == "ADMIN";
    public bool CanAccessPos => IsOwnerOrAdmin || RoleCode == "BRANCH_MANAGER" || RoleCode == "CASHIER";
    public bool CanAccessKds => IsOwnerOrAdmin || RoleCode == "BRANCH_MANAGER" || RoleCode == "BARISTA";
    public bool CanAccessInventory => IsOwnerOrAdmin || RoleCode == "BRANCH_MANAGER" || RoleCode == "INVENTORY_STAFF";

    public string DefaultView
    {
        get
        {
            if (RoleCode == "BARISTA") return "KDS";
            if (RoleCode == "INVENTORY_STAFF") return "INVENTORY";
            return "POS";
        }
    }
}
