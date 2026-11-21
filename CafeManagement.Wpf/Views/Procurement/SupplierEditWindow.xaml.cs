using System.Windows;
using CafeManagement.Core.DTOs.Procurement;

namespace CafeManagement.Wpf.Views.Procurement;

public partial class SupplierEditWindow : Window
{
    public SupplierDto? ResultSupplier { get; private set; }
    public CreateOrUpdateSupplierRequest RequestData { get; private set; } = new();
    private readonly int? _editingSupplierId;

    public SupplierEditWindow(SupplierDto? existing = null)
    {
        InitializeComponent();

        if (existing != null)
        {
            _editingSupplierId = existing.SupplierId;
            TxtTitle.Text = $"🏢 CẬP NHẬT NHÀ CUNG CẤP #{existing.SupplierCode}";
            TxtCode.Text = existing.SupplierCode;
            TxtName.Text = existing.SupplierName;
            TxtContact.Text = existing.ContactPerson ?? string.Empty;
            TxtPhone.Text = existing.Phone ?? string.Empty;
            TxtEmail.Text = existing.Email ?? string.Empty;
            TxtAddress.Text = existing.Address ?? string.Empty;
            TxtTaxCode.Text = existing.TaxCode ?? string.Empty;
            TxtNote.Text = existing.Note ?? string.Empty;
            ChkIsActive.IsChecked = existing.IsActive;
        }
        else
        {
            TxtTitle.Text = "🏢 THÊM MỚI NHÀ CUNG CẤP";
            TxtCode.Text = $"NCC-{DateTime.Now:yyMMddHHmm}";
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Vui lòng nhập Tên Nhà Cung Cấp.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        RequestData = new CreateOrUpdateSupplierRequest
        {
            SupplierCode = TxtCode.Text.Trim(),
            SupplierName = name,
            ContactPerson = TxtContact.Text.Trim(),
            Phone = TxtPhone.Text.Trim(),
            Email = TxtEmail.Text.Trim(),
            Address = TxtAddress.Text.Trim(),
            TaxCode = TxtTaxCode.Text.Trim(),
            Note = TxtNote.Text.Trim(),
            IsActive = ChkIsActive.IsChecked ?? true
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
