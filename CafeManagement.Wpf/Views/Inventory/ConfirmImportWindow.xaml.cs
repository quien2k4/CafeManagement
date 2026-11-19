using System.Windows;

namespace CafeManagement.Wpf.Views.Inventory;

public partial class ConfirmImportWindow : Window
{
    public ConfirmImportWindow(string ingredientName, decimal quantity, string uom, string formula, string note)
    {
        InitializeComponent();

        TxtIngredientName.Text = ingredientName;
        TxtQuantity.Text = $"+{quantity:N1}";
        TxtUom.Text = uom;
        TxtNote.Text = string.IsNullOrWhiteSpace(note) ? "Không có ghi chú" : note;

        if (!string.IsNullOrWhiteSpace(formula))
        {
            TxtFormula.Text = formula;
            PnlFormula.Visibility = Visibility.Visible;
        }
        else
        {
            PnlFormula.Visibility = Visibility.Collapsed;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
