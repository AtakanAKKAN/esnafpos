using EsnafPos.ViewModels;
using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EsnafPos.Views
{
    public partial class TablesPage : Page
    {
        private readonly TablesViewModel _vm;
        private readonly DispatcherTimer _elapsedTimer;
        private bool _tablesLoaded = false;

        public TablesPage(TablesViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            _vm.TableSelected += OnTableSelected;

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _elapsedTimer.Tick += (s, e) => RefreshElapsedTexts();

            var syncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            syncTimer.Tick += async (s, e) =>
            {
                if (App.Client != null)
                {
                    bool online = await App.Client.TestConnectionAsync();
                    Dispatcher.Invoke(() =>
                    {
                        PanelOfflineWarning.Visibility = online
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    });
                    if (online)
                        await _vm.LoadTablesCommand.ExecuteAsync(null);
                }
                else if (App.Server != null && IsVisible)
                {
                    await _vm.RefreshTablesFromDb();
                }
            };
            syncTimer.Start();

            Loaded += async (s, e) =>
            {
                _elapsedTimer.Start();

                // Lisans uyarı banner'ını göster
                ShowLicenseBanner();

                if (!_tablesLoaded)
                {
                    _tablesLoaded = true;
                    await _vm.LoadTablesCommand.ExecuteAsync(null);
                }
            };

            IsVisibleChanged += async (s, e) =>
            {
                if (!(bool)e.NewValue || !_tablesLoaded) return;

                if (App.Client != null)
                {
                    bool online = await App.Client.TestConnectionAsync();
                    PanelOfflineWarning.Visibility = online
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                    if (!online) return;
                }
                await _vm.LoadTablesCommand.ExecuteAsync(null);
            };

            Unloaded += (s, e) => _elapsedTimer.Stop();
        }

        // ─── LİSANS UYARI BANNER'I ───────────────────────────────────
        private void ShowLicenseBanner()
        {
#if DEBUG
            return; // Debug modda gösterme
#else
            var license = App.Services.GetRequiredService<LicenseService>();
            if (license.ExpiryDate == DateTime.MaxValue) return;

            var daysLeft = (license.ExpiryDate - DateTime.Now).TotalDays;

            if (daysLeft > 7)
            {
                PanelLicenseWarning.Visibility = Visibility.Collapsed;
                return;
            }

            PanelLicenseWarning.Visibility = Visibility.Visible;

            if (daysLeft <= 1)
            {
                PanelLicenseWarning.Background = new SolidColorBrush(Color.FromRgb(231, 76, 60));   // Kırmızı
                TxtLicenseWarning.Text = "🚨 Lisansınız yarın sona eriyor! Lütfen satıcınızla iletişime geçin.";
            }
            else if (daysLeft <= 3)
            {
                PanelLicenseWarning.Background = new SolidColorBrush(Color.FromRgb(211, 84, 0));    // Turuncu
                TxtLicenseWarning.Text = $"⚠ Lisansınız {(int)daysLeft} gün içinde sona erecek. Lütfen satıcınızla iletişime geçin.";
            }
            else
            {
                PanelLicenseWarning.Background = new SolidColorBrush(Color.FromRgb(243, 156, 18));  // Sarı
                TxtLicenseWarning.Text = $"⚠ Lisansınız {(int)daysLeft} gün içinde sona erecek.";
            }
#endif
        }

        private void RefreshElapsedTexts()
        {
            foreach (var card in _vm.AllCards)
            {
                if (card is Table t && t.LastItemAddedAt.HasValue)
                    t.ElapsedText = TablesViewModel.CalcElapsed(t.LastItemAddedAt.Value);
                if (card is VeresiyeCardItem v && v.LastItemAddedAt.HasValue)
                    v.ElapsedText = TablesViewModel.CalcElapsed(v.LastItemAddedAt.Value);
            }
        }

        private void OnTableSelected(Table table)
        {
            var orderVm = App.Services.GetRequiredService<OrderViewModel>();
            bool isReturn = table.Status == TableStatus.Active
                         || table.Status == TableStatus.PaymentPending;
            NavigationService?.Navigate(new OrderPage(orderVm, table, isReturn));
        }

        private void TableButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Table table)
                _vm.SelectTableCommand.Execute(table);
        }

        private async void VeresiyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VeresiyeCardItem card) return;

            var db = App.Services.GetRequiredService<AppDbContext>();
            var window = new VeresiyeDetailWindow(db, card);
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();

            if (window.AnyCollected)
                await _vm.RefreshVeresiye();
        }
    }
}
