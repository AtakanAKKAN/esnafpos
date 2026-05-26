using System.Windows;

namespace EsnafPos.Views
{
    public partial class ReasonDialog : Window
    {
        public string? SelectedReason { get; private set; }

        public ReasonDialog(string productName, int quantity)
        {
            InitializeComponent();
            TxtProductInfo.Text = $"{productName}  —  {quantity} adet eksiltiliyor";
        }

        private void BtnDegisim_Click(object sender, RoutedEventArgs e)
        {
            SelectedReason = "Urun Degisimi";
            DialogResult = true;
        }

        private void BtnIptal_Click(object sender, RoutedEventArgs e)
        {
            SelectedReason = "Urun Iptali";
            DialogResult = true;
        }
    }
}
