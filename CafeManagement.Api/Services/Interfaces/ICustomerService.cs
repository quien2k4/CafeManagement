using CafeManagement.Core.DTOs.Customers;

namespace CafeManagement.Api.Services.Interfaces;

public interface ICustomerService
{
    Task<List<CustomerDto>> GetCustomersAsync(string? search = null, string? tier = null);
    Task<CustomerDto?> GetCustomerByPhoneAsync(string phone);
    Task<CustomerDto?> GetCustomerByIdAsync(int id);
    Task<CustomerDto> CreateCustomerAsync(CustomerCreateRequest req);
    Task<CustomerDto> AdjustPointsAsync(AdjustCustomerPointsRequest req);
}
