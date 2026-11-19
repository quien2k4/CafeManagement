namespace CafeManagement.Core.DTOs.Employees;

public class EmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, INACTIVE
    public DateTime HireDate { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
}

public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}

public class EmployeeCreateRequest
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = "123456";
    public int RoleId { get; set; }
    public int BranchId { get; set; }
}

public class EmployeeUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? CitizenId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string Username { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int BranchId { get; set; }
    public bool ResetPassword { get; set; } = false;
}

public class ResetPasswordRequest
{
    public int UserId { get; set; }
    public string NewPassword { get; set; } = "123456";
}
