using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Api.Services.Interfaces;

public interface IBranchesService
{
    Task<List<BranchDto>> GetAllBranchesAsync(bool activeOnly = false);
    Task<List<BranchDto>> GetActiveBranchesAsync();
    Task<BranchDto?> GetBranchByIdAsync(int id);
    Task<BranchDto> CreateBranchAsync(BranchCreateRequest req);
    Task<BranchDto?> UpdateBranchAsync(int id, BranchUpdateRequest req);
    Task<bool> ToggleBranchActiveAsync(int id);
}
