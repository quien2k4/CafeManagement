using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CafeManagement.Wpf.ViewModels;

public partial class CheckoutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _orderNumber = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeAmount))]
    [NotifyPropertyChangedFor(nameof(IsCashValid))]
    private decimal _finalAmount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCashMode))]
    [NotifyPropertyChangedFor(nameof(IsQrMode))]
    private string _paymentMethod = "CASH"; // CASH, VIETQR

    public bool IsCashMode => PaymentMethod == "CASH";
    public bool IsQrMode => PaymentMethod == "VIETQR";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChangeAmount))]    
    [NotifyPropertyChangedFor(nameof(IsCashValid))]
    [NotifyPropertyChangedFor(nameof(CustomerGivenAmountDisplay))]
    private decimal _customerGivenAmount;

    public string CustomerGivenAmountDisplay => CustomerGivenAmount.ToString("N0") + " đ";

    public decimal ChangeAmount => CustomerGivenAmount >= FinalAmount ? CustomerGivenAmount - FinalAmount : 0;
    public bool IsCashValid => PaymentMethod != "CASH" || CustomerGivenAmount >= FinalAmount;

    public string BankName => "VietinBank";
    public string AccountNumber => "1098 7654 3210";
    public string AccountHolder => "CHUOI CAFE SAO MOI";

    public CheckoutViewModel(string orderNumber, decimal finalAmount)
    {
        _orderNumber = orderNumber;
        _finalAmount = finalAmount;
        _customerGivenAmount = finalAmount; // Mặc định khách đưa vừa đủ tiền
    }

    [RelayCommand]
    public void SelectCash()
    {
        PaymentMethod = "CASH";
    }

    [RelayCommand]
    public void SelectQr()
    {
        PaymentMethod = "VIETQR";
    }

    [RelayCommand]
    public void SetQuickCash(object? parameter)
    {
        if (parameter != null && decimal.TryParse(parameter.ToString(), out decimal amount))
        {
            if (amount == 0)
            {
                CustomerGivenAmount = FinalAmount;
            }
            else
            {
                CustomerGivenAmount = amount;
            }
        }
    }

    [RelayCommand]
    public void AddQuickCash(object? parameter)
    {
        if (parameter != null && decimal.TryParse(parameter.ToString(), out decimal amount))
        {
            CustomerGivenAmount += amount;
        }
    }
}
