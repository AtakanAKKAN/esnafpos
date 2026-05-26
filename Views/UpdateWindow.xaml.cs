using EsnafPos.Services;
using System.Windows;

namespace EsnafPos.Views
{
    public partial class UpdateWindow : Window
    {
        private readonly UpdateService      _updateService;
        private readonly UpdateCheckResult  _result;

        public UpdateWindow(UpdateService updateService, UpdateCheckResult result)
        {
            InitializeComponent();
            _updateService = updateService;
            _result        = result;

            TxtVersion.Text      = $"Sürüm {result.VersionNumber}";
            TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "Bu sürümde iyileştirmeler ve hata düzeltmeleri yapılmıştır."
                : result.ReleaseNotes;

            if (result.IsForced)
            {
                PanelForced.Visibility = Visibility.Visible;
                BtnLater.IsEnabled     = false;
                BtnLater.Opacity       = 0.4;
            }
        }

        private void BtnLater_Click(object sender, RoutedEventArgs e)
            => Close();

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdate.IsEnabled  = false;
            BtnLater.IsEnabled   = false;
            TxtUpdateBtn.Text    = "İndiriliyor...";
            ProgressBar.Visibility  = Visibility.Visible;
            TxtProgress.Visibility  = Visibility.Visible;

            var progress = new Progress<int>(p =>
            {
                ProgressBar.Value = p;
                TxtProgress.Text  = $"%{p} tamamlandı...";
            });

            var success = await _updateService.DownloadAndInstallAsync(
                _result.DownloadUrl!, progress);

            if (!success)
            {
                MessageBox.Show(
                    "Güncelleme indirilemedi. Lütfen tekrar deneyin.",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnUpdate.IsEnabled = true;
                BtnLater.IsEnabled  = !_result.IsForced;
                TxtUpdateBtn.Text   = "Güncelle ve Yeniden Başlat";
                ProgressBar.Visibility = Visibility.Collapsed;
                TxtProgress.Visibility = Visibility.Collapsed;
            }
        }
    }
}
