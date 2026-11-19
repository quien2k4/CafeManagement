using CafeManagement.Core.DTOs.Employees;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class EmployeeApiClient
{
    private static readonly Lazy<EmployeeApiClient> _instance = new(() => new EmployeeApiClient());
    public static EmployeeApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public EmployeeApiClient() : this(ApiClient.Instance)
    {
    }

    public EmployeeApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        try
        {
            var list = await _api.GetAsync<List<EmployeeDto>>("/api/employees");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<List<Role>> GetRolesAsync()
    {
        try
        {
            var list = await _api.GetAsync<List<RoleDto>>("/api/employees/roles");
            return list?.Select(r => new Role
            {
                RoleId = r.RoleId,
                RoleCode = r.RoleCode,
                RoleName = r.RoleName
            }).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<EmployeeDto?> CreateEmployeeAsync(EmployeeCreateRequest req)
    {
        return await _api.PostAsync<EmployeeCreateRequest, EmployeeDto>("/api/employees", req);
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(int id, EmployeeUpdateRequest req)
    {
        return await _api.PutAsync<EmployeeUpdateRequest, EmployeeDto>($"/api/employees/{id}", req);
    }

    public async Task<bool> ToggleStatusAsync(int id)
    {
        try
        {
            return await _api.PostAsync($"/api/employees/{id}/toggle-status", new { });
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword = "123456")
    {
        try
        {
            return await _api.PostAsync("/api/employees/reset-password", new ResetPasswordRequest
            {
                UserId = userId,
                NewPassword = newPassword
            });
        }
        catch
        {
            return false;
        }
    }
}
