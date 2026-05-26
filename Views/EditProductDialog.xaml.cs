using EsnafPos.Models;
using System.Windows;

namespace EsnafPos.Views
{
    public partial class EditProductDialog : Window
    {
        public string ProductName   { get; private set; } = "";
        public string PriceAz      { get; private set; } = "";
        public string PriceTam     { get; private set; } = "";
        public string PriceBucuk   { get; private set; } = "";
        public bool   Saved        { get; private set; } = false;

        public EditProductDialog(Product product)
        {
            InitializeComponent();
            RunProductName.Text = product.Name;
            TxtName.Text        = product.Name;
            TxtAz.Text          = product.PriceAz.HasValue    ? product.PriceAz.Value.ToString("N2")    : "";
            TxtTam.Text         = product.PriceTam > 0 ? product.PriceTam.ToString("N2") : "";
            TxtBucuk.Text       = product.PriceBucuk.HasValue ? product.PriceBucuk.Value.ToString("N2") : "";

            Loaded += (s, e) => TxtName.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // TAM fiyat zorunlu
            if (string.IsNullOrWhiteSpace(TxtTam.Text))
            {
                TxtTam.BorderBrush = System.Windows.Media.Brushes.Red;
                TxtTam.BorderThickness = new Thickness(2);
                TxtTam.Focus();
                return;
            }

            ProductName  = TxtName.Text.Trim();
            PriceAz      = TxtAz.Text.Trim();
            PriceTam     = TxtTam.Text.Trim();
            PriceBucuk   = TxtBucuk.Text.Trim();
            Saved        = true;
            DialogResult = true;
        }

        private void TxtPrice_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (TxtAz == null || TxtTam == null || TxtBucuk == null) return;
            // TAM alanı doldurulunca kırmızı border'ı temizle
            if (sender == TxtTam && !string.IsNullOrWhiteSpace(TxtTam.Text))
            {
                TxtTam.ClearValue(System.Windows.Controls.TextBox.BorderBrushProperty);
                TxtTam.ClearValue(System.Windows.Controls.TextBox.BorderThicknessProperty);
            }
            var azOk  = decimal.TryParse(TxtAz.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var az);
            var tamOk = decimal.TryParse(TxtTam.Text.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var tam);

            if (azOk && tamOk && (az > 0 || tam > 0))
                TxtBucuk.Text = (az + tam).ToString("N2");
        }

        private void Txt_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                BtnSave_Click(sender, e);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
