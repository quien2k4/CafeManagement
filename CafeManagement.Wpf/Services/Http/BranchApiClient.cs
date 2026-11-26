using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Wpf.Services.Http;

public class BranchApiClient
{
    private static readonly Lazy<BranchApiClient> _instance = new(() => new BranchApiClient());
    public static BranchApiClient Instance => _instance.Value;

    private readonly ApiClient _api;

    public BranchApiClient() : this(ApiClient.Instance)
    {
    }

    public BranchApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<List<BranchDto>> GetBranchesAsync(bool activeOnly = false)
    {
        try
        {
            var list = await _api.GetAsync<List<BranchDto>>($"/api/branches?activeOnly={activeOnly}");
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<BranchDto?> GetBranchByIdAsync(int id)
    {
        return await _api.GetAsync<BranchDto>($"/api/branches/{id}");
    }

    public async Task<BranchDto?> CreateBranchAsync(BranchCreateRequest req)
    {
        return await _api.PostAsync<BranchCreateRequest, BranchDto>("/api/branches", req);
    }

    public async Task<BranchDto?> UpdateBranchAsync(int id, BranchUpdateRequest req)
    {
        return await _api.PutAsync<BranchUpdateRequest, BranchDto>($"/api/branches/{id}", req);
    }

    public async Task<bool> ToggleBranchActiveAsync(int id)
    {
        try
        {
            return await _api.PostAsync($"/api/branches/{id}/toggle-active", new { });
        }
        catch
        {
            return false;
        }
    }
}
