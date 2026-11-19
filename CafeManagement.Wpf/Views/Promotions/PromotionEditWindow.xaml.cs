using System.Windows;
using System.Windows.Controls;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.Views.Promotions;

public partial class PromotionEditWindow : Window
{
    public Promotion? PromoToSave { get; private set; }
    public bool IsNew { get; }

    public PromotionEditWindow(Promotion? existing = null)
    {
        InitializeComponent();

        if (existing == null)
        {
            IsNew = true;
            TxtTitle.Text = "➕ TẠO VOUCHER / KHUYẾN MÃI MỚI";
            TxtCode.Text = $"PROMO_{Random.Shared.Next(100, 999)}";
        }
        else
        {
            IsNew = false;
            PromoToSave = existing;
            TxtTitle.Text = $"✏️ CHỈNH SỬA VOUCHER: {existing.PromotionCode}";
            TxtCode.Text = existing.PromotionCode;
            TxtCode.IsEnabled = false;
            TxtName.Text = existing.PromotionName;
            TxtDiscountValue.Text = existing.DiscountValue.ToString("N0");
            TxtMaxDiscount.Text = existing.MaxDiscountAmount?.ToString("N0") ?? "";
            TxtMinOrder.Text = existing.MinOrderAmount.ToString("N0");
            TxtUsageLimit.Text = existing.UsageLimit?.ToString() ?? "";
            ChkIsActive.IsChecked = existing.IsActive;

            foreach (ComboBoxItem item in CboDiscountType.Items)
            {
                if (item.Tag?.ToString() == existing.DiscountType)
                {
                    CboDiscountType.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string code = TxtCode.Text.Trim().ToUpper();
        string name = TxtName.Text.Trim();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Vui lòng nhập đầy đủ mã và tên chương trình khuyến mãi!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string discType = (CboDiscountType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "PERCENT";

        string rawDiscVal = TxtDiscountValue.Text.Replace(",", "").Replace(".", "").Trim();
        if (!decimal.TryParse(rawDiscVal, out decimal discVal) || discVal < 0)
        {
            MessageBox.Show("Giá trị giảm giá không hợp lệ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal? maxDisc = null;
        string rawMaxDisc = TxtMaxDiscount.Text.Replace(",", "").Replace(".", "").Trim();
        if (decimal.TryParse(rawMaxDisc, out decimal md) && md > 0)
        {
            maxDisc = md;
        }

        string rawMinOrder = TxtMinOrder.Text.Replace(",", "").Replace(".", "").Trim();
        decimal.TryParse(rawMinOrder, out decimal minOrder);

        int? usageLimit = null;
        string rawUsage = TxtUsageLimit.Text.Trim();
        if (int.TryParse(rawUsage, out int ul) && ul > 0)
        {
            usageLimit = ul;
        }

        if (IsNew)
        {
            PromoToSave = new Promotion
            {
                PromotionCode = code,
                PromotionName = name,
                DiscountType = discType,
                DiscountValue = discVal,
                MaxDiscountAmount = maxDisc,
                MinOrderAmount = minOrder,
                UsageLimit = usageLimit,
                UsedCount = 0,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddMonths(3),
                IsActive = ChkIsActive.IsChecked == true
            };
        }
        else if (PromoToSave != null)
        {
            PromoToSave.PromotionName = name;
            PromoToSave.DiscountType = discType;
            PromoToSave.DiscountValue = discVal;
            PromoToSave.MaxDiscountAmount = maxDisc;
            PromoToSave.MinOrderAmount = minOrder;
            PromoToSave.UsageLimit = usageLimit;
            PromoToSave.IsActive = ChkIsActive.IsChecked == true;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
