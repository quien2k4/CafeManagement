namespace CafeManagement.Core.DTOs.Authentication;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int BranchId { get; set; }
}
