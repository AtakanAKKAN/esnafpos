using EsnafPos.Services;
using EsnafPos.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using System.Globalization;

namespace EsnafPos.Views
{
    public partial class ReportsPage : Page
    {
        private readonly ReportsViewModel _vm;
        private readonly ExcelExportService _excel;
        private readonly SessionService _session;

        private readonly SolidColorBrush _activeColor;
        private readonly SolidColorBrush _inactiveColor;
        private readonly SolidColorBrush _activeText;
        private readonly SolidColorBrush _inactiveText;

        public ReportsPage(ReportsViewModel vm, ExcelExportService excel, SessionService session)
        {
            InitializeComponent();
            _vm = vm;
            _excel = excel;
            _session = session;
            DataContext = vm;

            _activeColor   = (SolidColorBrush)FindResource("AccentBrush");
            _inactiveColor = new SolidColorBrush(Colors.Transparent);
            _activeText    = new SolidColorBrush(Colors.White);
            _inactiveText  = (SolidColorBrush)FindResource("PrimaryBrush");

            if (!_session.IsAdmin)
            {
                PanelDailyReport.Visibility   = Visibility.Collapsed;
                PanelWeeklyReport.Visibility  = Visibility.Collapsed;
                PanelMonthlyReport.Visibility = Visibility.Collapsed;
                PanelTabs.Visibility          = Visibility.Collapsed;
                BtnChangeLog.Visibility       = Visibility.Collapsed;
            }

            Loaded += async (s, e) =>
            {
                if (_session.IsAdmin)
                {
                    // İstemci modunda raporlar sunucudan çekilemiyor
                    if (App.Client != null)
                    {
                        PanelClientMode.Visibility = Visibility.Visible;
                        return;
                    }
                    SetActiveTab("daily");
                    _vm.SelectedDate = DateTime.Today;
                    UpdateDailyDateLabel();
                    await _vm.LoadDailyReportCommand.ExecuteAsync(null);
                }
            };
        }

        private void SetActiveTab(string tab)
        {
            // Tum panelleri gizle
            PanelDailyReport.Visibility   = Visibility.Collapsed;
            PanelWeeklyReport.Visibility  = Visibility.Collapsed;
            PanelMonthlyReport.Visibility = Visibility.Collapsed;

            // Tum buton iceriklerini guncelle
            SetTabButton(BtnTabDaily,   "📅  Gunluk",   tab == "daily");
            SetTabButton(BtnTabWeekly,  "📆  Haftalik",  tab == "weekly");
            SetTabButton(BtnTabMonthly, "🗓️  Aylik",     tab == "monthly");

            // Secilen paneli goster
            switch (tab)
            {
                case "daily":   PanelDailyReport.Visibility   = Visibility.Visible; break;
                case "weekly":  PanelWeeklyReport.Visibility  = Visibility.Visible; break;
                case "monthly": PanelMonthlyReport.Visibility = Visibility.Visible; break;
            }
        }

        private void SetTabButton(Button btn, string text, bool isActive)
        {
            btn.Background = isActive ? _activeColor : _inactiveColor;
            btn.Content = new TextBlock
            {
                Text       = text,
                FontSize   = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = isActive ? _activeText : _inactiveText,
                Margin     = new System.Windows.Thickness(6, 0, 6, 0)
            };
        }

        private static readonly CultureInfo TrCulture = new CultureInfo("tr-TR");

        private void UpdateDailyDateLabel()
        {
            if (TxtDailyDateLabel != null)
                TxtDailyDateLabel.Text = _vm.SelectedDate.ToString("dd MMMM yyyy", TrCulture);
        }

        private async void BtnPreviousDay_Click(object sender, RoutedEventArgs e)
        {
            _vm.SelectedDate = _vm.SelectedDate.AddDays(-1);
            UpdateDailyDateLabel();
            await _vm.LoadDailyReportCommand.ExecuteAsync(null);
        }

        private async void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedDate.Date < DateTime.Today)
            {
                _vm.SelectedDate = _vm.SelectedDate.AddDays(1);
                UpdateDailyDateLabel();
                await _vm.LoadDailyReportCommand.ExecuteAsync(null);
            }
        }

        private async void BtnTabDaily_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("daily");
            _vm.SelectedDate = DateTime.Today;
            UpdateDailyDateLabel();
            await _vm.LoadDailyReportCommand.ExecuteAsync(null);
        }

        private async void BtnTabWeekly_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("weekly");
            await _vm.LoadWeeklyReportCommand.ExecuteAsync(null);
        }

        private async void BtnTabMonthly_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("monthly");
            await _vm.LoadMonthlyReportCommand.ExecuteAsync(null);
        }

        private void BtnOrderHistory_Click(object sender, RoutedEventArgs e)
        {
            var db      = App.Services.GetRequiredService<EsnafPos.Data.AppDbContext>();
            var printer = App.Services.GetRequiredService<PrinterService>();
            var window  = new OrderHistoryWindow(db, printer);
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }

        private void BtnChangeLog_Click(object sender, RoutedEventArgs e)
        {
            var db     = App.Services.GetRequiredService<EsnafPos.Data.AppDbContext>();
            var window = new ChangeLogWindow(db);
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }

        private void BtnExportDaily_Click(object sender, RoutedEventArgs e)
            => _excel.ExportDailyReport(_vm);

        private void BtnExportWeekly_Click(object sender, RoutedEventArgs e)
            => _excel.ExportWeeklyReport(_vm);

        private void BtnExportMonthly_Click(object sender, RoutedEventArgs e)
            => _excel.ExportMonthlyReport(_vm);
    }
}
