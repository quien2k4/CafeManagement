using CafeManagement.Core.DTOs.Employees;

namespace CafeManagement.Api.Services.Interfaces;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetEmployeesAsync();
    Task<List<RoleDto>> GetRolesAsync();
    Task<EmployeeDto> CreateEmployeeAsync(EmployeeCreateRequest req);
    Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeUpdateRequest req);
    Task<bool> ToggleStatusAsync(int employeeId);
    Task<bool> ResetPasswordAsync(int userId, string newPassword);
}
