using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CafeManagement.Core.Models;

namespace CafeManagement.Wpf.ViewModels;

public partial class VariantSelectionItem : ObservableObject
{
    public ProductVariant Variant { get; set; } = null!;

    [ObservableProperty]
    private bool _isSelected;
}

public partial class ToppingSelectionItem : ObservableObject
{
    public Topping Topping { get; set; } = null!;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    private int _quantity = 1;

    public decimal TotalPrice => Topping.Price * Quantity;
}

public partial class CustomizeProductViewModel : ObservableObject
{
    [ObservableProperty]
    private Product _product;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private ObservableCollection<VariantSelectionItem> _variantItems = [];

    [ObservableProperty]
    private string _selectedSugar = "100%";

    [ObservableProperty]
    private string _selectedIce = "100%";

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPrice))]
    private int _quantity = 1;

    [ObservableProperty]
    private ObservableCollection<ToppingSelectionItem> _toppings = [];

    public ProductVariant? SelectedVariant => VariantItems.FirstOrDefault(v => v.IsSelected)?.Variant;

    public decimal UnitPrice
    {
        get
        {
            decimal baseP = Product.BasePrice;
            decimal extraP = SelectedVariant?.ExtraPrice ?? 0;
            decimal toppingsP = Toppings.Where(t => t.IsSelected).Sum(t => t.Topping.Price * t.Quantity);
            return baseP + extraP + toppingsP;
        }
    }

    public decimal TotalPrice => UnitPrice * Quantity;

    public CustomizeProductViewModel(Product product, List<Topping> availableToppings, CartItemViewModel? editItem = null)
    {
        _product = product;
        _isEditMode = editItem != null;

        // 1. Khởi tạo danh sách Size và chỉ chọn duy nhất 1 Size (mặc định là Size M)
        var variants = new List<VariantSelectionItem>();
        int targetVariantId = editItem != null ? editItem.Variant.VariantId : 0;

        foreach (var v in product.Variants)
        {
            var item = new VariantSelectionItem { Variant = v };
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(VariantSelectionItem.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectedVariant));
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(TotalPrice));
                }
            };
            variants.Add(item);
        }

        if (editItem != null)
        {
            var selected = variants.FirstOrDefault(v => v.Variant.VariantId == targetVariantId) ?? variants.FirstOrDefault();
            if (selected != null) selected.IsSelected = true;
        }
        else
        {
            // Mặc định chọn Size M
            var sizeM = variants.FirstOrDefault(v => v.Variant.Size.Equals("M", StringComparison.OrdinalIgnoreCase)) ?? variants.FirstOrDefault();
            if (sizeM != null) sizeM.IsSelected = true;
        }

        _variantItems = new ObservableCollection<VariantSelectionItem>(variants);

        // 2. Khởi tạo tùy chọn Đường, Đá, Ghi chú, Số lượng
        if (editItem != null)
        {
            _selectedSugar = editItem.SugarLevel;
            _selectedIce = editItem.IceLevel;
            _note = editItem.Note;
            _quantity = editItem.Quantity;
        }

        // 3. Khởi tạo Topping kèm số lượng (mặc định 1 khi tích chọn)
        var toppingItems = new List<ToppingSelectionItem>();
        foreach (var t in availableToppings)
        {
            var item = new ToppingSelectionItem { Topping = t, IsSelected = false, Quantity = 1 };

            if (editItem != null)
            {
                var existingTop = editItem.Toppings.FirstOrDefault(top => top.Topping.ToppingId == t.ToppingId);
                if (existingTop != null)
                {
                    item.IsSelected = true;
                    item.Quantity = existingTop.Quantity > 0 ? existingTop.Quantity : 1;
                }
            }

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ToppingSelectionItem.IsSelected))
                {
                    if (item.IsSelected && item.Quantity < 1)
                    {
                        item.Quantity = 1;
                    }
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(TotalPrice));
                }
                else if (e.PropertyName == nameof(ToppingSelectionItem.Quantity))
                {
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(TotalPrice));
                }
            };
            toppingItems.Add(item);
        }

        _toppings = new ObservableCollection<ToppingSelectionItem>(toppingItems);
    }

    [RelayCommand]
    public void SelectVariant(VariantSelectionItem selectedItem)
    {
        foreach (var v in VariantItems)
        {
            v.IsSelected = (v == selectedItem);
        }
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(TotalPrice));
    }

    [RelayCommand]
    public void SelectSugar(string sugar) => SelectedSugar = sugar;

    [RelayCommand]
    public void SelectIce(string ice) => SelectedIce = ice;

    [RelayCommand]
    public void IncreaseQuantity() => Quantity++;

    [RelayCommand]
    public void DecreaseQuantity()
    {
        if (Quantity > 1) Quantity--;
    }

    [RelayCommand]
    public void IncreaseTopping(ToppingSelectionItem item)
    {
        item.Quantity++;
        if (!item.IsSelected) item.IsSelected = true;
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(TotalPrice));
    }

    [RelayCommand]
    public void DecreaseTopping(ToppingSelectionItem item)
    {
        if (item.Quantity > 1)
        {
            item.Quantity--;
        }
        else
        {
            item.IsSelected = false;
            item.Quantity = 1;
        }
        OnPropertyChanged(nameof(UnitPrice));
        OnPropertyChanged(nameof(TotalPrice));
    }
}
