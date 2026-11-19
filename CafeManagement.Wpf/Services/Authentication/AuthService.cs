using System.Security.Cryptography;
using System.Text;
using CafeManagement.Core.Common.Models;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Services.Authentication;

public class AuthService
{
    public static string HashMd5(string input)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    public static async Task<UserSession?> AuthenticateAsync(string username, string password, int selectedBranchId)
    {
        return await AuthenticationApiClient.Instance.LoginAsync(username, password, selectedBranchId);
    }

    public static async Task EnsureAuthSeedDataAsync()
    {
        try
        {
            await AuthenticationApiClient.Instance.SeedAsync();
        }
        catch
        {
            // Bỏ qua lỗi kết nối nền
        }
    }
}
