namespace CafeManagement.Core.DTOs.Branches;

public class BranchDto
{
    public int BranchId { get; set; }
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public string DisplayName => $"{BranchName} ({BranchCode})";
}
