using EsnafPos.Models;
using System.Windows;

namespace EsnafPos.Views
{
    public partial class PortionWindow : Window
    {
        public string? SelectedPortion { get; private set; }
        public decimal SelectedPrice { get; private set; }

        private readonly Product _product;

        public PortionWindow(Product product)
        {
            InitializeComponent();
            _product = product;

            TxtProductName.Text = product.Name;
            TxtTamPrice.Text = $"{product.PriceTam:N2} TL";

            if (product.PriceAz.HasValue)
            {
                BtnAz.Visibility = Visibility.Visible;
                TxtAzPrice.Text = $"{product.PriceAz.Value:N2} TL";
            }

            if (product.PriceBucuk.HasValue)
            {
                BtnBucuk.Visibility = Visibility.Visible;
                TxtBucukPrice.Text = $"{product.PriceBucuk.Value:N2} TL";
            }
        }

        private void BtnAz_Click(object sender, RoutedEventArgs e)
        {
            SelectedPortion = "Az";
            SelectedPrice = _product.PriceAz!.Value;
            DialogResult = true;
        }

        private void BtnTam_Click(object sender, RoutedEventArgs e)
        {
            SelectedPortion = "Tam";
            SelectedPrice = _product.PriceTam;
            DialogResult = true;
        }

        private void BtnBucuk_Click(object sender, RoutedEventArgs e)
        {
            SelectedPortion = "1.5 Porsiyon";
            SelectedPrice = _product.PriceBucuk!.Value;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}