using CafeManagement.Core.Common.Models;
using CafeManagement.Core.DTOs.Authentication;
using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Wpf.Services.Http;

public class AuthenticationApiClient
{
    private static readonly Lazy<AuthenticationApiClient> _instance = new(() => new AuthenticationApiClient());
    public static AuthenticationApiClient Instance => _instance.Value;

    private readonly ApiClient _apiClient;

    public AuthenticationApiClient() : this(ApiClient.Instance)
    {
    }

    public AuthenticationApiClient(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        var branches = await _apiClient.GetAsync<List<BranchDto>>("/api/branches");
        return branches ?? [];
    }

    public async Task<UserSession?> LoginAsync(string username, string password, int branchId)
    {
        var req = new LoginRequest
        {
            Username = username,
            Password = password,
            BranchId = branchId
        };
        return await _apiClient.PostAsync<LoginRequest, UserSession>("/api/authentication/login", req);
    }

    public async Task SeedAsync()
    {
        await _apiClient.PostAsync("/api/authentication/seed", new { });
    }
}
