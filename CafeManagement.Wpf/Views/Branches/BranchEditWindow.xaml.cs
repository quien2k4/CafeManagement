using System.Windows;
using CafeManagement.Core.DTOs.Branches;
using CafeManagement.Wpf.Services.Http;

namespace CafeManagement.Wpf.Views.Branches;

public partial class BranchEditWindow : Window
{
    private readonly BranchDto? _editingBranch;
    public BranchDto? SavedBranch { get; private set; }

    public BranchEditWindow(BranchDto? branch = null)
    {
        InitializeComponent();
        _editingBranch = branch;

        if (_editingBranch != null)
        {
            Title = "Chỉnh Sửa Thông Tin Chi Nhánh";
            TxtHeaderTitle.Text = $"✏️ CHỈNH SỬA CHI NHÁNH: {_editingBranch.BranchCode}";
            TxtBranchCode.Text = _editingBranch.BranchCode;
            TxtBranchCode.IsEnabled = false; // Không cho sửa mã chi nhánh đã có
            TxtBranchName.Text = _editingBranch.BranchName;
            TxtAddress.Text = _editingBranch.Address;
            TxtPhone.Text = _editingBranch.Phone;
        }
        else
        {
            Title = "Thêm Mới Chi Nhánh Chuỗi";
            TxtHeaderTitle.Text = "🏢 THÊM MỚI CHI NHÁNH CHUỖI";
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtBranchCode.Text.Trim().ToUpper();
        string name = TxtBranchName.Text.Trim();
        string address = TxtAddress.Text.Trim();
        string phone = TxtPhone.Text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show("Vui lòng nhập Mã chi nhánh (VD: BR03)!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtBranchCode.Focus();
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập Tên chi nhánh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtBranchName.Focus();
            return;
        }

        if (string.IsNullOrEmpty(address))
        {
            MessageBox.Show("Vui lòng nhập Địa chỉ chi nhánh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAddress.Focus();
            return;
        }

        if (string.IsNullOrEmpty(phone))
        {
            MessageBox.Show("Vui lòng nhập Số điện thoại hotline chi nhánh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPhone.Focus();
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            if (_editingBranch == null)
            {
                // Tạo mới chi nhánh
                var req = new BranchCreateRequest
                {
                    BranchCode = code,
                    BranchName = name,
                    Address = address,
                    Phone = phone
                };

                var created = await BranchApiClient.Instance.CreateBranchAsync(req);
                if (created != null)
                {
                    SavedBranch = created;
                    MessageBox.Show($"🎉 Đã thêm thành công chi nhánh '{created.BranchName}' ({created.BranchCode}) vào hệ thống chuỗi!\nĐã tự động khởi tạo kho nguyên liệu ban đầu cho chi nhánh.", 
                        "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Không thể tạo chi nhánh. Vui lòng kiểm tra lại kết nối mạng hoặc xem mã chi nhánh có bị trùng không.", 
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Cập nhật chi nhánh
                var req = new BranchUpdateRequest
                {
                    BranchName = name,
                    Address = address,
                    Phone = phone
                };

                var updated = await BranchApiClient.Instance.UpdateBranchAsync(_editingBranch.BranchId, req);
                if (updated != null)
                {
                    SavedBranch = updated;
                    MessageBox.Show($"✅ Đã cập nhật thành công thông tin chi nhánh '{updated.BranchName}'!", 
                        "Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Không thể cập nhật thông tin chi nhánh.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi thao tác: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSave.IsEnabled = true;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
