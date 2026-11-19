using CafeManagement.Core.DTOs.Branches;

namespace CafeManagement.Api.Services.Interfaces;

public interface IBranchesService
{
    Task<List<BranchDto>> GetActiveBranchesAsync();
}
