using Microsoft.EntityFrameworkCore;
using CafeManagement.Api.Data;
using CafeManagement.Api.Services.Interfaces;
using CafeManagement.Core.DTOs.Customers;
using CafeManagement.Core.Models;

namespace CafeManagement.Api.Services.Customers;

public class CustomerService : ICustomerService
{
    private readonly CafeDbContext _db;

    public CustomerService(CafeDbContext db)
    {
        _db = db;
    }

    public async Task<List<CustomerDto>> GetCustomersAsync(string? search = null, string? tier = null)
    {
        var query = _db.Customers.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            string keyword = search.Trim().ToLower();
            query = query.Where(c => c.PhoneNumber.Contains(keyword) || c.FullName.ToLower().Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(tier) && tier != "ALL")
        {
            query = query.Where(c => c.MembershipTier == tier);
        }

        var list = await query.OrderByDescending(c => c.CustomerId).ToListAsync();
        return list.Select(MapToDto).ToList();
    }

    public async Task<CustomerDto?> GetCustomerByPhoneAsync(string phone)
    {
        string p = phone.Trim();
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PhoneNumber == p && c.IsActive);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == id && c.IsActive);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CustomerCreateRequest req)
    {
        string phone = req.PhoneNumber.Trim();
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == phone);
        if (existing != null)
        {
            throw new InvalidOperationException($"Số điện thoại {phone} đã được đăng ký cho khách hàng '{existing.FullName}'!");
        }

        var customer = new Customer
        {
            PhoneNumber = phone,
            FullName = req.FullName.Trim(),
            MembershipTier = "BRONZE",
            TotalPoints = 0,
            LifetimePoints = 0,
            TotalSpent = 0,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        return MapToDto(customer);
    }

    public async Task<CustomerDto> AdjustPointsAsync(AdjustCustomerPointsRequest req)
    {
        var customer = await _db.Customers.FindAsync(req.CustomerId)
            ?? throw new InvalidOperationException($"Không tìm thấy khách hàng ID={req.CustomerId}!");

        int newPoints = customer.TotalPoints + req.PointAdjustment;
        if (newPoints < 0)
        {
            throw new InvalidOperationException($"Điểm sau điều chỉnh ({newPoints}) không thể âm!");
        }

        customer.TotalPoints = newPoints;
        if (req.PointAdjustment > 0)
        {
            customer.LifetimePoints += req.PointAdjustment;
        }

        UpdateTier(customer);
        await _db.SaveChangesAsync();

        return MapToDto(customer);
    }

    public static void UpdateTier(Customer customer)
    {
        customer.MembershipTier = customer.LifetimePoints switch
        {
            >= 600 => "DIAMOND",
            >= 300 => "GOLD",
            >= 100 => "SILVER",
            _ => "BRONZE"
        };
    }

    private static CustomerDto MapToDto(Customer c)
    {
        return new CustomerDto
        {
            CustomerId = c.CustomerId,
            PhoneNumber = c.PhoneNumber,
            FullName = c.FullName,
            MembershipTier = c.MembershipTier,
            TotalPoints = c.TotalPoints,
            LifetimePoints = c.LifetimePoints,
            TotalSpent = c.TotalSpent,
            CreatedAt = c.CreatedAt,
            IsActive = c.IsActive
        };
    }
}
