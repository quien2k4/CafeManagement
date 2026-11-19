using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Authentication;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Employees;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly CafeDbContext _db;

    public EmployeeService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        var emps = await _db.Employees
            .AsNoTracking()
            .OrderBy(e => e.EmployeeId)
            .ToListAsync();

        var users = await _db.UserAccounts
            .AsNoTracking()
            .ToListAsync();

        var assignments = await _db.UserRoleBranchAssignments
            .AsNoTracking()
            .Include(a => a.Role)
            .Include(a => a.Branch)
            .ToListAsync();

        var list = new List<EmployeeDto>();
        foreach (var emp in emps)
        {
            var user = users.FirstOrDefault(u => u.EmployeeId == emp.EmployeeId);
            var assign = user != null ? assignments.FirstOrDefault(a => a.UserId == user.UserId) : null;

            list.Add(new EmployeeDto
            {
                EmployeeId = emp.EmployeeId,
                EmployeeCode = emp.EmployeeCode,
                FullName = emp.FullName,
                Phone = emp.Phone,
                Email = emp.Email,
                CitizenId = emp.CitizenId,
                Status = emp.Status,
                HireDate = emp.HireDate,
                UserId = user?.UserId,
                Username = user?.Username ?? string.Empty,
                RoleId = assign?.RoleId,
                RoleCode = assign?.Role?.RoleCode ?? string.Empty,
                RoleName = assign?.Role?.RoleName ?? "Chưa phân vai trò",
                BranchId = assign?.BranchId,
                BranchName = assign?.Branch?.BranchName ?? "Chưa gán chi nhánh"
            });
        }

        return list;
    }

    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.RoleId).ToListAsync();
        return roles.Select(r => new RoleDto
        {
            RoleId = r.RoleId,
            RoleCode = r.RoleCode,
            RoleName = r.RoleName
        }).ToList();
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(EmployeeCreateRequest req)
    {
        string username = req.Username.Trim().ToLower();
        if (await _db.UserAccounts.AnyAsync(u => u.Username.ToLower() == username))
        {
            throw new InvalidOperationException($"Tên đăng nhập '{req.Username}' đã tồn tại!");
        }

        var emp = new Employee
        {
            EmployeeCode = req.EmployeeCode.Trim(),
            FullName = req.FullName.Trim(),
            Phone = req.Phone.Trim(),
            Email = req.Email?.Trim(),
            CitizenId = req.CitizenId?.Trim(),
            Status = req.Status,
            HireDate = DateTime.UtcNow
        };

        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();

        string salt = "SALT_CAFE_2026";
        string pwdHash = AuthenticationService.HashMd5(string.IsNullOrWhiteSpace(req.Password) ? "123456" : req.Password);

        var user = new UserAccount
        {
            EmployeeId = emp.EmployeeId,
            Username = username,
            PasswordHash = pwdHash,
            PasswordSalt = salt,
            IsActive = req.Status == "ACTIVE"
        };
        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync();

        var assignment = new UserRoleBranchAssignment
        {
            UserId = user.UserId,
            RoleId = req.RoleId,
            BranchId = req.BranchId,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        };
        _db.UserRoleBranchAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        var role = await _db.Roles.FindAsync(req.RoleId);
        var branch = await _db.Branches.FindAsync(req.BranchId);

        return new EmployeeDto
        {
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Phone = emp.Phone,
            Email = emp.Email,
            CitizenId = emp.CitizenId,
            Status = emp.Status,
            HireDate = emp.HireDate,
            UserId = user.UserId,
            Username = user.Username,
            RoleId = req.RoleId,
            RoleCode = role?.RoleCode ?? string.Empty,
            RoleName = role?.RoleName ?? string.Empty,
            BranchId = req.BranchId,
            BranchName = branch?.BranchName ?? string.Empty
        };
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(int id, EmployeeUpdateRequest req)
    {
        var emp = await _db.Employees.FindAsync(id);
        if (emp == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy nhân viên ID {id}");
        }

        string username = req.Username.Trim().ToLower();
        var existingUser = await _db.UserAccounts.FirstOrDefaultAsync(u => u.EmployeeId == id);

        if (await _db.UserAccounts.AnyAsync(u => u.Username.ToLower() == username && (existingUser == null || u.UserId != existingUser.UserId)))
        {
            throw new InvalidOperationException($"Tên đăng nhập '{req.Username}' đã được sử dụng bởi nhân viên khác!");
        }

        emp.FullName = req.FullName.Trim();
        emp.Phone = req.Phone.Trim();
        emp.Email = req.Email?.Trim();
        emp.CitizenId = req.CitizenId?.Trim();
        emp.Status = req.Status;

        if (existingUser == null)
        {
            existingUser = new UserAccount
            {
                EmployeeId = emp.EmployeeId,
                Username = username,
                PasswordHash = AuthenticationService.HashMd5("123456"),
                PasswordSalt = "SALT_CAFE_2026",
                IsActive = emp.Status == "ACTIVE",
                IsLocked = emp.Status != "ACTIVE"
            };
            _db.UserAccounts.Add(existingUser);
            await _db.SaveChangesAsync();
        }
        else
        {
            existingUser.Username = username;
            existingUser.IsActive = emp.Status == "ACTIVE";
            existingUser.IsLocked = emp.Status != "ACTIVE";
            if (req.ResetPassword)
            {
                existingUser.PasswordHash = AuthenticationService.HashMd5("123456");
            }
        }

        // Cập nhật phân quyền
        var oldAssignments = await _db.UserRoleBranchAssignments.Where(a => a.UserId == existingUser.UserId).ToListAsync();
        _db.UserRoleBranchAssignments.RemoveRange(oldAssignments);

        _db.UserRoleBranchAssignments.Add(new UserRoleBranchAssignment
        {
            UserId = existingUser.UserId,
            RoleId = req.RoleId,
            BranchId = req.BranchId,
            IsActive = true,
            AssignedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        var role = await _db.Roles.FindAsync(req.RoleId);
        var branch = await _db.Branches.FindAsync(req.BranchId);

        return new EmployeeDto
        {
            EmployeeId = emp.EmployeeId,
            EmployeeCode = emp.EmployeeCode,
            FullName = emp.FullName,
            Phone = emp.Phone,
            Email = emp.Email,
            CitizenId = emp.CitizenId,
            Status = emp.Status,
            HireDate = emp.HireDate,
            UserId = existingUser.UserId,
            Username = existingUser.Username,
            RoleId = req.RoleId,
            RoleCode = role?.RoleCode ?? string.Empty,
            RoleName = role?.RoleName ?? string.Empty,
            BranchId = req.BranchId,
            BranchName = branch?.BranchName ?? string.Empty
        };
    }

    public async Task<bool> ToggleStatusAsync(int employeeId)
    {
        var emp = await _db.Employees.FindAsync(employeeId);
        if (emp == null) return false;

        emp.Status = emp.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";

        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.EmployeeId == employeeId);
        if (user != null)
        {
            user.IsActive = emp.Status == "ACTIVE";
            user.IsLocked = emp.Status != "ACTIVE";
        }

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
    {
        var user = await _db.UserAccounts.FindAsync(userId);
        if (user == null) return false;

        user.PasswordHash = AuthenticationService.HashMd5(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }
}
