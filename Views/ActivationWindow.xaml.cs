using EsnafPos.Services;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public partial class ActivationWindow : Window
    {
        private readonly LicenseService _license;

        public ActivationWindow(LicenseService license)
        {
            InitializeComponent();
            _license = license;
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            var code = TxtCode.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(code))
            {
                ShowError("Lütfen aktivasyon kodunu girin.");
                return;
            }

            // Format kontrolü: CIKO-XXXX-XXXX
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$"))
            {
                ShowError("Kod formatı geçersiz. Örnek: CIKO-XXXX-XXXX");
                return;
            }

            SetLoading(true);
            TxtError.Visibility = Visibility.Collapsed;

            var (success, error) = await _license.ActivateAsync(code);

            SetLoading(false);

            if (success)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError(error);
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text       = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void SetLoading(bool loading)
        {
            BtnActivate.IsEnabled = !loading;
            TxtBtnLabel.Text      = loading ? "Kontrol ediliyor..." : "Aktive Et";
            TxtCode.IsEnabled     = !loading;
        }
    }
}
