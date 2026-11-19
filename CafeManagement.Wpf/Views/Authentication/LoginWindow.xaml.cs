using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.Common.Models;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Core.Models;
using CafeManagement.Wpf.Services.Authentication;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Authentication;

public partial class LoginWindow : Window
{
    public UserSession? AuthenticatedSession { get; private set; }

    public LoginWindow()
    {
        InitializeComponent();
        _ = LoadBranchesAsync();
    }

    private async Task LoadBranchesAsync()
    {
        try
        {
            var branches = await AuthenticationApiClient.Instance.GetBranchesAsync();

            if (branches.Count == 0)
            {
                branches = GetDefaultBranches();
            }

            CboBranches.ItemsSource = branches;
            CboBranches.SelectedIndex = 0;
            TxtError.Visibility = Visibility.Collapsed;
            TxtError.Text = string.Empty;
        }
        catch (Exception ex)
        {
            var defaultBranches = GetDefaultBranches();
            CboBranches.ItemsSource = defaultBranches;
            CboBranches.SelectedIndex = 0;

            if (ex is System.Net.Http.HttpRequestException || ex.Message.Contains("refused") || ex.Message.Contains("kết nối"))
            {
                TxtError.Text = "⚠️ Chưa kết nối được đến máy chủ API (http://localhost:5000).\nVui lòng khởi động project CafeManagement.Api!";
            }
            else
            {
                TxtError.Text = $"Lưu ý kết nối: {ex.Message}";
            }
            TxtError.Visibility = Visibility.Visible;
        }
    }

    private static List<BranchDto> GetDefaultBranches()
    {
        return new List<BranchDto>
        {
            new() { BranchId = 1, BranchCode = "BR01", BranchName = "Chi nhánh Quận 1 (BR01)", Address = "123 Lê Lợi, Q1, TP.HCM", Phone = "02838111111" },
            new() { BranchId = 2, BranchCode = "BR02", BranchName = "Chi nhánh Bình Thạnh (BR02)", Address = "475A Điện Biên Phủ, Bình Thạnh, TP.HCM", Phone = "02838222222" }
        };
    }

    private void QuickSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string username)
        {
            TxtUsername.Text = username;
            TxtPassword.Password = "123456";
            TxtError.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Password.Trim();

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            TxtError.Text = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu!";
            TxtError.Visibility = Visibility.Visible;
            return;
        }

        int branchId = (CboBranches.SelectedItem as BranchDto)?.BranchId 
                    ?? (CboBranches.SelectedItem as Branch)?.BranchId 
                    ?? 1;

        BtnLogin.IsEnabled = false;
        BtnLogin.Content = "Đang kiểm tra đăng nhập...";
        TxtError.Visibility = Visibility.Collapsed;

        try
        {
            var session = await AuthService.AuthenticateAsync(username, password, branchId);
            if (session == null)
            {
                TxtError.Text = "Tên đăng nhập hoặc mật khẩu không chính xác!";
                TxtError.Visibility = Visibility.Visible;
                BtnLogin.IsEnabled = true;
                BtnLogin.Content = "ĐĂNG NHẬP VÀO HỆ THỐNG ➔";
                return;
            }

            AuthenticatedSession = session;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            if (ex is System.Net.Http.HttpRequestException || ex.Message.Contains("refused") || ex.Message.Contains("kết nối"))
            {
                TxtError.Text = "⚠️ Không thể kết nối tới máy chủ API (http://localhost:5000).\nVui lòng kiểm tra và khởi động CafeManagement.Api!";
            }
            else
            {
                TxtError.Text = $"Lỗi đăng nhập: {ex.Message}";
            }
            TxtError.Visibility = Visibility.Visible;
            BtnLogin.IsEnabled = true;
            BtnLogin.Content = "ĐĂNG NHẬP VÀO HỆ THỐNG ➔";
        }
    }
}
