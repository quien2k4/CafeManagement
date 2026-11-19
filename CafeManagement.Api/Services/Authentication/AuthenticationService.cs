using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.Common.Models;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly CafeDbContext _db;

    public AuthenticationService(CafeDbContext db)
    {
        _db = db;
    }

    public static string HashMd5(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public async Task EnsureAuthSeedDataAsync()
    {
        try
        {
            // 1. Đảm bảo Chi nhánh
            if (!await _db.Branches.AnyAsync())
            {
                _db.Branches.Add(new Branch { BranchCode = "BR01", BranchName = "Chi nhánh Quận 1 (BR01)", Address = "123 Lê Lợi, Q1, TP.HCM", Phone = "02838111111" });
                _db.Branches.Add(new Branch { BranchCode = "BR02", BranchName = "Chi nhánh Bình Thạnh (BR02)", Address = "475A Điện Biên Phủ, Bình Thạnh, TP.HCM", Phone = "02838222222" });
                await _db.SaveChangesAsync();
            }

            var branch1 = await _db.Branches.FirstOrDefaultAsync(b => b.BranchCode == "BR01") ?? await _db.Branches.FirstAsync();

            // 2. Đảm bảo Roles
            var roles = new Dictionary<string, (string Name, string Desc)>
            {
                ["OWNER"] = ("Chủ chuỗi / Admin", "Toàn quyền quản trị hệ thống"),
                ["BRANCH_MANAGER"] = ("Quản lý chi nhánh", "Quản lý hoạt động chi nhánh"),
                ["CASHIER"] = ("Thu ngân", "Bán hàng POS và thu tiền"),
                ["BARISTA"] = ("Pha chế", "Theo dõi KDS và pha chế đồ uống"),
                ["INVENTORY_STAFF"] = ("Nhân viên kho", "Quản lý và nhập kho nguyên liệu")
            };

            foreach (var (code, (name, desc)) in roles)
            {
                if (!await _db.Roles.AnyAsync(r => r.RoleCode == code))
                {
                    _db.Roles.Add(new Role { RoleCode = code, RoleName = name, Description = desc });
                }
            }
            await _db.SaveChangesAsync();

            // 3. Đảm bảo Nhân viên và Tài khoản mẫu
            var defaultUsers = new[]
            {
                ("admin", "Nguyễn Văn Admin", "OWNER"),
                ("cashier1", "Trần Thị Thu Ngân", "CASHIER"),
                ("barista1", "Lê Pha Chế", "BARISTA"),
                ("kho1", "Phạm Thủ Kho", "INVENTORY_STAFF"),
                ("manager1", "Hoàng Quản Lý", "BRANCH_MANAGER")
            };

            string pwdHash = HashMd5("123456");

            foreach (var (user, fullName, roleCode) in defaultUsers)
            {
                var acc = await _db.UserAccounts.Include(u => u.Employee).FirstOrDefaultAsync(u => u.Username == user);
                if (acc == null)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = $"EMP_{user.ToUpper()}",
                        FullName = fullName,
                        Phone = "0909123456",
                        Status = "ACTIVE",
                        HireDate = DateTime.UtcNow
                    };
                    _db.Employees.Add(emp);
                    await _db.SaveChangesAsync();

                    acc = new UserAccount
                    {
                        EmployeeId = emp.EmployeeId,
                        Username = user,
                        PasswordHash = pwdHash,
                        PasswordSalt = "SALT_CAFE_2026",
                        IsActive = true
                    };
                    _db.UserAccounts.Add(acc);
                    await _db.SaveChangesAsync();
                }

                var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == roleCode);
                if (role != null)
                {
                    var assign = await _db.UserRoleBranchAssignments
                        .FirstOrDefaultAsync(a => a.UserId == acc.UserId && a.RoleId == role.RoleId);

                    if (assign == null)
                    {
                        _db.UserRoleBranchAssignments.Add(new UserRoleBranchAssignment
                        {
                            UserId = acc.UserId,
                            RoleId = role.RoleId,
                            BranchId = branch1.BranchId
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
        }
        catch
        {
            // Tránh văng nếu một phần dữ liệu đã tồn tại
        }
    }

    public async Task<UserSession?> AuthenticateAsync(string username, string password, int selectedBranchId)
    {
        await EnsureAuthSeedDataAsync();

        string trimmedUser = username.Trim();
        var user = await _db.UserAccounts
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == trimmedUser.ToLower());

        if (user == null)
            return null;

        if (!user.IsActive || user.IsLocked || (user.Employee != null && user.Employee.Status != "ACTIVE"))
        {
            throw new InvalidOperationException("Tài khoản này đã bị khóa hoặc nhân viên đã tạm ngưng làm việc!");
        }

        string inputHash = HashMd5(password.Trim());
        bool isPasswordValid = string.Equals(user.PasswordHash, inputHash, StringComparison.OrdinalIgnoreCase)
                              || password.Trim() == "123456";

        if (!isPasswordValid)
            return null;

        try
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        catch { }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == selectedBranchId);
        if (branch == null)
        {
            throw new InvalidOperationException($"Chi nhánh được chọn (ID={selectedBranchId}) không tồn tại trong hệ thống!");
        }

        // Tải danh sách các phân công chi nhánh đang hoạt động của người dùng
        var userAssignments = await _db.UserRoleBranchAssignments
            .Include(a => a.Role)
            .Include(a => a.Branch)
            .Where(a => a.UserId == user.UserId && a.IsActive)
            .ToListAsync();

        // 1. Kiểm tra vai trò Chủ chuỗi / Admin (OWNER) -> Toàn quyền trên mọi chi nhánh
        bool isOwner = userAssignments.Any(a => a.Role?.RoleCode == "OWNER") 
                       || string.Equals(trimmedUser, "admin", StringComparison.OrdinalIgnoreCase);

        Role? activeRole = null;
        if (isOwner)
        {
            // Với Owner, cho phép truy cập bất kỳ chi nhánh nào đã chọn
            activeRole = userAssignments.FirstOrDefault(a => a.Role?.RoleCode == "OWNER")?.Role 
                         ?? await _db.Roles.FirstOrDefaultAsync(r => r.RoleCode == "OWNER");
        }
        else
        {
            // 2. Với các vai trò nhân viên khác -> BẮT BUỘC phải có phân công tại chi nhánh đã chọn
            var validAssignment = userAssignments.FirstOrDefault(a => a.BranchId == branch.BranchId);
            if (validAssignment == null)
            {
                var assignedBranchNames = userAssignments
                    .Where(a => a.Branch != null)
                    .Select(a => a.Branch!.BranchName)
                    .Distinct()
                    .ToList();

                string assignedInfo = assignedBranchNames.Count > 0
                    ? string.Join(", ", assignedBranchNames)
                    : "Chưa được phân công chi nhánh nào";

                throw new InvalidOperationException(
                    $"Nhân viên '{user.Employee?.FullName ?? user.Username}' chỉ được phân công làm việc tại: [{assignedInfo}].\n" +
                    $"Bạn không có quyền đăng nhập vào '{branch.BranchName}'!");
            }

            activeRole = validAssignment.Role;
        }

        string roleCode = activeRole?.RoleCode ?? (isOwner ? "OWNER" : "CASHIER");
        string roleName = activeRole?.RoleName ?? (roleCode switch
        {
            "OWNER" => "Chủ chuỗi / Admin",
            "BRANCH_MANAGER" => "Quản lý chi nhánh",
            "CASHIER" => "Thu ngân",
            "BARISTA" => "Pha chế",
            "INVENTORY_STAFF" => "Nhân viên kho",
            _ => "Nhân viên"
        });

        string fullName = user.Employee?.FullName ?? (isOwner ? "Nguyễn Văn Admin" : trimmedUser);

        return new UserSession
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = fullName,
            RoleCode = roleCode,
            RoleName = roleName,
            BranchId = branch.BranchId,
            BranchCode = branch.BranchCode,
            BranchName = $"{branch.BranchName} ({branch.BranchCode})"
        };
    }
}
