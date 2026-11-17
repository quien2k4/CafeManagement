using System.Windows;
using CafeManagement.Wpf.Models;

namespace CafeManagement.Wpf.Views;

public partial class SelectPromotionWindow : Window
{
    public Promotion? SelectedPromotion { get; private set; }

    public SelectPromotionWindow(List<Promotion> promotions)
    {
        InitializeComponent();
        LstPromotions.ItemsSource = promotions;
    }

    private void ApplyPromo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is Promotion promo)
        {
            SelectedPromotion = promo;
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}