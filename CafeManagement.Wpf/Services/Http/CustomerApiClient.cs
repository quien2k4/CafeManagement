using CafeManagement.Core.DTOs.Customers;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Services.Http;

public class CustomerApiClient
{
    private static readonly Lazy<CustomerApiClient> _instance = new(() => new CustomerApiClient());
    public static CustomerApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public CustomerApiClient() : this(ApiClient.Instance)
    {
    }

    public CustomerApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<Customer>> GetCustomersAsync(string? search = null, string? tier = null)
    {
        string url = "/api/customers";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(tier)) queryParams.Add($"tier={Uri.EscapeDataString(tier)}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        var dtos = await _api.GetAsync<List<CustomerDto>>(url);
        return dtos?.Select(MapToCustomer).ToList() ?? [];
    }

    public async Task<Customer?> GetCustomerByPhoneAsync(string phone)
    {
        try
        {
            var dto = await _api.GetAsync<CustomerDto>($"/api/customers/by-phone/{Uri.EscapeDataString(phone.Trim())}");
            return dto == null ? null : MapToCustomer(dto);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Customer?> CreateCustomerAsync(CustomerCreateRequest req)
    {
        var dto = await _api.PostAsync<CustomerCreateRequest, CustomerDto>("/api/customers", req);
        return dto == null ? null : MapToCustomer(dto);
    }

    public async Task<Customer?> AdjustPointsAsync(AdjustCustomerPointsRequest req)
    {
        var dto = await _api.PostAsync<AdjustCustomerPointsRequest, CustomerDto>("/api/customers/adjust-points", req);
        return dto == null ? null : MapToCustomer(dto);
    }

    private static Customer MapToCustomer(CustomerDto d)
    {
        return new Customer
        {
            CustomerId = d.CustomerId,
            PhoneNumber = d.PhoneNumber,
            FullName = d.FullName,
            MembershipTier = d.MembershipTier,
            TotalPoints = d.TotalPoints,
            LifetimePoints = d.LifetimePoints,
            TotalSpent = d.TotalSpent,
            CreatedAt = d.CreatedAt,
            IsActive = d.IsActive
        };
    }
}
