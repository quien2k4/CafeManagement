using CafeManagement.Core.Common.Models;
using CafeManagement.Core.DTOs.Authentication;

namespace CafeManagement.Api.Services.Interfaces;

public interface IAuthenticationService
{
    Task EnsureAuthSeedDataAsync();
    Task<UserSession?> AuthenticateAsync(string username, string password, int selectedBranchId);
}
