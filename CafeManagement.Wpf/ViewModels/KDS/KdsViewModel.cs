using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CafeManagement.Wpf.ViewModels.KDS;

public partial class KdsItemViewModel : ObservableObject
{
    public long OrderItemId { get; set; }
    public long OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Size { get; set; } = "M";
    public int Quantity { get; set; } = 1;
    public string OptionsSummary { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplay))]
    [NotifyPropertyChangedFor(nameof(IsQueued))]
    [NotifyPropertyChangedFor(nameof(IsPreparing))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    private string _status = "QUEUED"; // QUEUED, PREPARING, READY, SERVED

    public string StatusDisplay => Status switch
    {
        "QUEUED" => "⏳ Chờ làm",
        "PREPARING" => "⚡ Đang pha",
        "READY" => "✅ Đã xong",
        "SERVED" => "🎉 Đã giao",
        _ => Status
    };

    public bool IsQueued => Status == "QUEUED";
    public bool IsPreparing => Status == "PREPARING";
    public bool IsReady => Status == "READY";
}

public partial class KdsTicketViewModel : ObservableObject
{
    public long OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string OrderType { get; set; } = "TAKE_AWAY";
    public DateTime CreatedAt { get; set; }

    public string OrderTypeText => OrderType == "TAKE_AWAY" ? "🛍️ Mang đi" : "🪑 Tại quán";

    public string WaitTimeText
    {
        get
        {
            var diff = DateTime.Now - CreatedAt;
            if (diff.TotalMinutes < 1) return "Vừa đặt (< 1 phút)";
            return $"Chờ {(int)diff.TotalMinutes} phút";
        }
    }

    [ObservableProperty]
    private string _orderStatus = "PAID";

    [ObservableProperty]
    private ObservableCollection<KdsItemViewModel> _items = [];

    public bool IsAllItemsReady => Items.All(i => i.Status == "READY" || i.Status == "SERVED");
}
