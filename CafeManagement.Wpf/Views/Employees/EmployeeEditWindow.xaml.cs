using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.Employees;

public partial class EmployeeEditWindow : Window
{
    public Employee? ResultEmployee { get; private set; }
    public string ResultUsername { get; private set; } = string.Empty;
    public int ResultRoleId { get; private set; }
    public int ResultBranchId { get; private set; }
    public bool ResetPasswordRequested { get; private set; }

    private readonly Employee? _originalEmployee;
    private readonly bool _isEditMode;

    public EmployeeEditWindow(List<Role> roles, List<BranchDto> branches, Employee? existingEmployee = null, string? username = null, int? roleId = null, int? branchId = null)
    {
        InitializeComponent();

        _originalEmployee = existingEmployee;
        _isEditMode = existingEmployee != null;

        CboRole.ItemsSource = roles;
        CboBranch.ItemsSource = branches;

        if (_isEditMode && existingEmployee != null)
        {
            TxtTitle.Text = $"✏️ CẬP NHẬT NHÂN VIÊN: {existingEmployee.FullName}";
            TxtEmployeeCode.Text = existingEmployee.EmployeeCode;
            TxtEmployeeCode.IsEnabled = false; // Không cho sửa mã NV
            TxtFullName.Text = existingEmployee.FullName;
            TxtPhone.Text = existingEmployee.Phone;
            TxtCitizenId.Text = existingEmployee.CitizenId ?? "";
            TxtEmail.Text = existingEmployee.Email ?? "";

            CboStatus.SelectedIndex = existingEmployee.Status == "ACTIVE" ? 0 : 1;

            if (roleId.HasValue) CboRole.SelectedValue = roleId.Value;
            else CboRole.SelectedIndex = 0;

            if (branchId.HasValue) CboBranch.SelectedValue = branchId.Value;
            else CboBranch.SelectedIndex = 0;

            TxtUsername.Text = !string.IsNullOrWhiteSpace(username) ? username : existingEmployee.EmployeeCode.ToLower();
            TxtUsername.IsEnabled = true; // Cho phép nhập hoặc cập nhật username
            ChkResetPassword.Visibility = Visibility.Visible;
        }
        else
        {
            TxtTitle.Text = "➕ THÊM MỚI NHÂN VIÊN & CẤP TÀI KHOẢN";
            string autoCode = $"EMP{DateTime.UtcNow:yyMM}-{Random.Shared.Next(100, 999)}";
            TxtEmployeeCode.Text = autoCode;
            TxtUsername.Text = autoCode.ToLower();

            CboRole.SelectedIndex = 0;
            CboBranch.SelectedIndex = 0;
            CboStatus.SelectedIndex = 0;
            ChkResetPassword.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtEmployeeCode.Text.Trim();
        string name = TxtFullName.Text.Trim();
        string phone = TxtPhone.Text.Trim();
        string username = TxtUsername.Text.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(username))
        {
            MessageBox.Show("Vui lòng điền đầy đủ Mã nhân viên, Họ tên, Số điện thoại và Tên đăng nhập!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CboRole.SelectedValue is not int roleId || CboBranch.SelectedValue is not int branchId)
        {
            MessageBox.Show("Vui lòng chọn Chức vụ và Chi nhánh làm việc!", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string status = (CboStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ACTIVE";

        if (_isEditMode && _originalEmployee != null)
        {
            _originalEmployee.FullName = name;
            _originalEmployee.Phone = phone;
            _originalEmployee.CitizenId = TxtCitizenId.Text.Trim();
            _originalEmployee.Email = TxtEmail.Text.Trim();
            _originalEmployee.Status = status;
            ResultEmployee = _originalEmployee;
        }
        else
        {
            ResultEmployee = new Employee
            {
                EmployeeCode = code,
                FullName = name,
                Phone = phone,
                CitizenId = TxtCitizenId.Text.Trim(),
                Email = TxtEmail.Text.Trim(),
                Status = status,
                HireDate = DateTime.UtcNow
            };
        }

        ResultUsername = username;
        ResultRoleId = roleId;
        ResultBranchId = branchId;
        ResetPasswordRequested = ChkResetPassword.IsChecked == true;

        DialogResult = true;
        Close();
    }
}
