using EsnafPos.Models;
using EsnafPos.Network;
using EsnafPos.Services;
using EsnafPos.ViewModels;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace EsnafPos.Views
{
    public partial class AdminPage : Page
    {
        private readonly AdminViewModel _vm;
        private readonly SettingsService _settings;
        private string _activeTab = "";
        private DispatcherTimer? _toastTimer;
        private string _activeChannelFilter = "";

        public AdminPage(AdminViewModel vm, SettingsService settings)
        {
            InitializeComponent();
            DataContext = vm;
            _vm = vm;
            _settings = settings;

            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_vm.EditingTable))
                    PanelEditTable.Visibility = _vm.EditingTable != null ? Visibility.Visible : Visibility.Collapsed;
                if (e.PropertyName == nameof(_vm.EditingUser))
                    PanelEditUser.Visibility = _vm.EditingUser != null ? Visibility.Visible : Visibility.Collapsed;
                if (e.PropertyName == nameof(_vm.EditingChannel))
                    PanelEditChannel.Visibility = _vm.EditingChannel != null ? Visibility.Visible : Visibility.Collapsed;
                if (e.PropertyName == nameof(_vm.EditingVeresiye) && _vm.EditingVeresiye == null)
                    PanelEditVeresiye.Visibility = Visibility.Collapsed;
                if (e.PropertyName == nameof(_vm.IsBusy))
                    LoadingIndicator.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
            };

            Loaded += async (s, e) =>
            {
                await _vm.LoadCommand.ExecuteAsync(null);
                SetActiveTab("masalar");

                if (App.Client != null)
                {
                    BtnNavMasalar.IsEnabled      = false;
                    BtnNavUrunler.IsEnabled      = false;
                    BtnNavKullanicilar.IsEnabled = false;
                    BtnNavKanallar.IsEnabled     = false;
                    BtnNavVeresiye.IsEnabled     = false;
                    SetActiveTab("ag");
                }
            };

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _toastTimer.Tick += (s, e) => { _toastTimer.Stop(); HideToast(); };
        }

        // ─── SIDEBAR NAVİGASYON ──────────────────────────────────

        private void BtnNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                SetActiveTab(tag);
        }

        private void SetActiveTab(string tab)
        {
            _activeTab = tab;

            bool isClient = App.Client != null;
            var dbTabs = new[] { "masalar", "urunler", "kullanicilar", "kanallar", "veresiye" };
            if (isClient && dbTabs.Contains(tab))
            {
                ShowToast("Bu sekme sadece Sunucu veya Tek Bilgisayar modunda kullanilabilir.", success: false);
                return;
            }

            PanelMasalar.Visibility       = Visibility.Collapsed;
            PanelUrunler.Visibility       = Visibility.Collapsed;
            PanelKullanicilar.Visibility  = Visibility.Collapsed;
            PanelKanallar.Visibility      = Visibility.Collapsed;
            PanelVeresiyeAdmin.Visibility = Visibility.Collapsed;
            PanelIsletme.Visibility       = Visibility.Collapsed;
            PanelAg.Visibility            = Visibility.Collapsed;
            PanelYazici.Visibility        = Visibility.Collapsed;

            SetNavStyle(BtnNavMasalar,      false);
            SetNavStyle(BtnNavUrunler,      false);
            SetNavStyle(BtnNavKullanicilar, false);
            SetNavStyle(BtnNavKanallar,     false);
            SetNavStyle(BtnNavVeresiye,     false);
            SetNavStyle(BtnNavIsletme,      false);
            SetNavStyle(BtnNavAg,           false);
            SetNavStyle(BtnNavYazici,       false);

            switch (tab)
            {
                case "masalar":
                    PanelMasalar.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavMasalar, true);
                    TxtPageTitle.Text    = "Masalar";
                    TxtPageSubtitle.Text = "Masa ekle, sil ve sirala";
                    break;
                case "urunler":
                    PanelUrunler.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavUrunler, true);
                    TxtPageTitle.Text    = "Urunler";
                    TxtPageSubtitle.Text = "Kategori ve urun yonetimi";
                    RefreshChannelFilter();
                    ApplyChannelFilter(_activeChannelFilter);
                    break;
                case "kullanicilar":
                    PanelKullanicilar.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavKullanicilar, true);
                    TxtPageTitle.Text    = "Kullanicilar";
                    TxtPageSubtitle.Text = "Kasiyer ve yonetici hesaplari";
                    // Rol ComboBox'ı doldur
                    if (CbNewUserRole.Items.Count == 0)
                    {
                        CbNewUserRole.Items.Add("Cashier");
                        CbNewUserRole.Items.Add("Admin");
                        CbNewUserRole.SelectedIndex = 0;
                    }
                    break;
                case "kanallar":
                    PanelKanallar.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavKanallar, true);
                    TxtPageTitle.Text    = "Kanallar";
                    TxtPageSubtitle.Text = "Siparis kanali ekle, sil, sirala";
                    break;
                case "veresiye":
                    PanelVeresiyeAdmin.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavVeresiye, true);
                    TxtPageTitle.Text    = "Veresiye";
                    TxtPageSubtitle.Text = "Aktif veresiye kayitlari";
                    _ = _vm.LoadVeresiyeCommand.ExecuteAsync(null);
                    break;
                case "isletme":
                    PanelIsletme.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavIsletme, true);
                    TxtPageTitle.Text    = "Isletme Ayarlari";
                    TxtPageSubtitle.Text = "Uygulama adi, fis bilgileri";
                    LoadBusinessSettings();
                    break;
                case "ag":
                    PanelAg.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavAg, true);
                    TxtPageTitle.Text    = "Ag Ayarlari";
                    TxtPageSubtitle.Text = "Sunucu ve istemci modu";
                    LoadNetworkSettings();
                    break;
                case "yazici":
                    PanelYazici.Visibility = Visibility.Visible;
                    SetNavStyle(BtnNavYazici, true);
                    TxtPageTitle.Text    = "Yazici";
                    TxtPageSubtitle.Text = "USB veya ag yazici baglantisi";
                    LoadPrinterSettings();
                    break;
            }
        }

        private void SetNavStyle(Button btn, bool active)
        {
            btn.Style = active
                ? (Style)FindResource("NavBtnActive")
                : (Style)FindResource("NavBtn");

            if (btn.Content is TextBlock tb)
                tb.Style = active
                    ? (Style)FindResource("NavBtnTextActive")
                    : (Style)FindResource("NavBtnText");
        }

        // ─── TOAST ────────────────────────────────────────────────

        private void ShowToast(string message, bool success = true)
        {
            ToastMessage.Text      = message;
            ToastIcon.Text         = success ? "✓" : "✕";
            ToastBorder.Background = new SolidColorBrush(
                success ? Color.FromRgb(39, 174, 96) : Color.FromRgb(231, 76, 60));
            var show = (System.Windows.Media.Animation.Storyboard)FindResource("ToastShow");
            show.Begin();
            _toastTimer?.Stop();
            _toastTimer?.Start();
        }

        private void HideToast()
        {
            var hide = (System.Windows.Media.Animation.Storyboard)FindResource("ToastHide");
            hide.Begin();
        }

        // ─── MASA ─────────────────────────────────────────────────

        private async void BtnAddTable_Click(object sender, RoutedEventArgs e)
        {
            _vm.NewTableName = TxtNewTableName.Text;
            await _vm.AddTableCommand.ExecuteAsync(null);
            TxtNewTableName.Text = "";
            ShowToast("Masa eklendi");
        }

        private void BtnStartEditTable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Table t)
                _vm.StartEditTableCommand.Execute(t);
        }

        private async void BtnSaveEditTable_Click(object sender, RoutedEventArgs e)
        {
            await _vm.SaveEditTableCommand.ExecuteAsync(null);
            ShowToast("Masa guncellendi");
        }

        private async void BtnDeleteTable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Table t) return;
            var r = MessageBox.Show($"'{t.Name}' silinecek. Devam?", "Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteTableCommand.ExecuteAsync(t);
            ShowToast("Masa silindi");
        }

        private async void BtnMoveTableUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Table t)
                await _vm.MoveTableUpCommand.ExecuteAsync(t);
        }

        private async void BtnMoveTableDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Table t)
                await _vm.MoveTableDownCommand.ExecuteAsync(t);
        }

        // ─── KATEGORİ / ÜRÜN ──────────────────────────────────────

        // Kanal filtre
        private void BtnChannelFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _activeChannelFilter = btn.Tag?.ToString() ?? "";
            RefreshChannelFilter(); // buton stillerini de günceller
        }

        // Kanal butonlarını dinamik oluştur — DB'den gelen kanallar
        private void RefreshChannelFilter()
        {
            SpChannelFilter.Children.Clear();

            if (!_vm.Channels.Any())
            {
                // Kanal yok → uyarı göster, kategori eklenemez
                TxtNoChannels.Visibility          = Visibility.Visible;
                TxtActiveCategoryChannel.Text     = "-";
                _activeChannelFilter              = "";
                ApplyChannelFilter("");
                return;
            }

            TxtNoChannels.Visibility = Visibility.Collapsed;

            // Aktif kanal geçerli değilse ilk kanala düş
            if (string.IsNullOrEmpty(_activeChannelFilter) ||
                !_vm.Channels.Any(c => c.Name == _activeChannelFilter))
                _activeChannelFilter = _vm.Channels.First().Name;

            foreach (var ch in _vm.Channels)
            {
                var isActive = ch.Name == _activeChannelFilter;
                var btn = new Button
                {
                    Tag     = ch.Name,
                    Height  = 34,
                    Padding = new Thickness(14, 0, 14, 0),
                    Margin  = new Thickness(0, 0, 6, 0),
                    Style   = (Style)FindResource(isActive ? "PrimaryButton" : "SecondaryButton")
                };
                btn.Content = new TextBlock { Text = ch.Name, Foreground = System.Windows.Media.Brushes.White, FontSize = 12 };
                btn.Click  += BtnChannelFilter_Click;
                SpChannelFilter.Children.Add(btn);
            }

            TxtActiveCategoryChannel.Text = _activeChannelFilter;
            ApplyChannelFilter(_activeChannelFilter);
        }

        private void ApplyChannelFilter(string channel)
        {
            var view = CollectionViewSource.GetDefaultView(_vm.Categories);
            if (string.IsNullOrEmpty(channel))
                view.Filter = null;
            else
                view.Filter = obj => (obj as Category)?.Channel == channel;
        }

        // Kategori ekle — aktif kanal filtresine göre eklenir
        private async void BtnAddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_activeChannelFilter))
            {
                ShowToast("Oncelikle Kanallar sekmesinden bir kanal ekleyin.", success: false);
                return;
            }
            _vm.NewCategoryName    = TxtNewCategoryName.Text;
            _vm.NewCategoryChannel = _activeChannelFilter;
            await _vm.AddCategoryCommand.ExecuteAsync(null);
            TxtNewCategoryName.Text = "";
            ShowToast("Kategori eklendi");
        }

        // Kategori düzenle — EditCategoryDialog açılır (butonun yakınında)
        private async void BtnStartEditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Category c) return;

            var dialog = new EditCategoryDialog(c, _vm.Channels.ToList()) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            _vm.StartEditCategoryCommand.Execute(c);
            _vm.EditCategoryName    = dialog.CategoryName;
            _vm.EditCategoryChannel = dialog.Channel;
            await _vm.SaveEditCategoryCommand.ExecuteAsync(null);
            ShowToast("Kategori guncellendi");
        }

        private async void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Category c) return;
            var r = MessageBox.Show($"'{c.Name}' ve tum urunleri silinecek!", "Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteCategoryCommand.ExecuteAsync(c);
            ShowToast("Kategori silindi");
        }

        // Ürün düzenle — EditProductDialog açılır (butonun yakınında)
        private async void BtnStartEditProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Product p) return;

            var dialog = new EditProductDialog(p) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;

            _vm.StartEditProductCommand.Execute(p);
            _vm.EditProductName       = dialog.ProductName;
            _vm.EditProductPriceTam   = dialog.PriceTam;
            _vm.EditProductPriceAz    = dialog.PriceAz;
            _vm.EditProductPriceBucuk = dialog.PriceBucuk;
            await _vm.SaveEditProductCommand.ExecuteAsync(null);
            ShowToast("Urun guncellendi");
        }

        // Ürün ekle — EditProductDialog boş ürünle açılır
        private async void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Category c) return;

            var emptyProduct = new Product { Name = "", PriceTam = 0 };
            var dialog = new EditProductDialog(emptyProduct)
            {
                Owner = Window.GetWindow(this),
                Title = "Yeni Urun Ekle"
            };
            if (dialog.ShowDialog() != true) return;

            // AddProduct komutu SelectedCategory + NewProduct alanlarını kullanır
            _vm.SelectedCategory       = c;
            _vm.NewProductName         = dialog.ProductName;
            _vm.NewProductPriceTam     = dialog.PriceTam;
            _vm.NewProductPriceAz      = dialog.PriceAz;
            _vm.NewProductPriceBucuk   = dialog.PriceBucuk;
            await _vm.AddProductCommand.ExecuteAsync(null);
            ShowToast("Urun eklendi");
        }

        private async void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not Product p) return;
            var r = MessageBox.Show($"'{p.Name}' silinecek. Devam?", "Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteProductCommand.ExecuteAsync(p);
            ShowToast("Urun silindi");
        }

        private async void BtnMoveCategoryUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Category c)
                await _vm.MoveCategoryUpCommand.ExecuteAsync(c);
        }

        private async void BtnMoveCategoryDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Category c)
                await _vm.MoveCategoryDownCommand.ExecuteAsync(c);
        }

        // ─── KULLANICI ────────────────────────────────────────────

        private void BtnStartEditUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is User u)
                _vm.StartEditUserCommand.Execute(u);
        }

        private async void BtnSaveEditUser_Click(object sender, RoutedEventArgs e)
        {
            _vm.EditUserPin = PbEditPin.Password;
            await _vm.SaveEditUserCommand.ExecuteAsync(null);
            PbEditPin.Password = "";
            ShowToast("Kullanici guncellendi");
        }

        private async void BtnAddUser_Click(object sender, RoutedEventArgs e)
        {
            _vm.NewUsername = TxtNewUserName.Text;
            _vm.NewUserPin  = PbNewPin.Password;
            _vm.NewUserRole = CbNewUserRole.SelectedItem?.ToString() ?? "Cashier";
            await _vm.AddUserCommand.ExecuteAsync(null);
            TxtNewUserName.Text = "";
            PbNewPin.Password   = "";
            ShowToast("Kullanici eklendi");
        }

        private async void BtnDeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not User u) return;
            var r = MessageBox.Show($"'{u.Username}' silinecek. Devam?", "Sil",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteUserCommand.ExecuteAsync(u);
            ShowToast("Kullanici silindi");
        }

        // ─── VERESİYE ─────────────────────────────────────────────

        private void BtnEditVeresiye_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VeresiyeAdminEntry entry) return;
            _vm.StartEditVeresiyeCommand.Execute(entry);
            PanelEditVeresiye.Visibility = Visibility.Visible;
        }

        private async void BtnSaveVeresiyeName_Click(object sender, RoutedEventArgs e)
        {
            await _vm.SaveVeresiyeNameCommand.ExecuteAsync(null);
            PanelEditVeresiye.Visibility = Visibility.Collapsed;
            ShowToast("Musteri adi guncellendi");
        }

        private async void BtnDeleteVeresiye_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VeresiyeAdminEntry entry) return;
            var r = MessageBox.Show(
                $"{entry.CustomerName} adli musterinin {entry.Amount:N2} TL veresiyesi iptal edilecek.",
                "Veresiye Iptal", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteVeresiyeEntryCommand.ExecuteAsync(entry);
            ShowToast("Veresiye iptal edildi");
        }

        // ─── İŞLETME AYARLARI ─────────────────────────────────────

        private void LoadBusinessSettings()
        {
            var b = _settings.Business;
            TxtAppName.Text         = b.AppName;
            TxtBusinessName.Text    = b.BusinessName;
            TxtBusinessAddress.Text = b.Address;
            TxtBusinessPhone.Text   = b.Phone;
            TxtReceiptNote.Text     = b.ReceiptNote;
            LoadLicenseInfo();
            LoadIconPreview();
        }

        private void LoadLicenseInfo()
        {
            // İstemci modunda lisans kontrolü yapılmaz — lisans bilgisi gösterme
            if (App.Client != null) return;
#if DEBUG
            PanelLicenseInfo.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
            TxtLicenseIcon.Text    = "🛠";
            TxtLicenseStatus.Text  = "Geliştirici Modu";
            TxtLicenseExpiry.Text  = "Lisans kontrolü devre dışı";
            TxtLicensePackage.Text = "Paket: Çok Ekran (debug)";
#else
            var license  = App.Services.GetRequiredService<LicenseService>();
            var daysLeft = (license.ExpiryDate - DateTime.Now).TotalDays;

            if (daysLeft <= 0)
            {
                PanelLicenseInfo.Background = new SolidColorBrush(Color.FromRgb(253, 237, 237));
                TxtLicenseIcon.Text    = "❌";
                TxtLicenseStatus.Text  = "Lisans Süresi Doldu";
                TxtLicenseExpiry.Text  = $"Bitiş: {license.ExpiryDate:dd MMMM yyyy}";
            }
            else if (daysLeft <= 7)
            {
                PanelLicenseInfo.Background = new SolidColorBrush(Color.FromRgb(255, 248, 225));
                TxtLicenseIcon.Text    = "⚠";
                TxtLicenseStatus.Text  = $"Lisans {(int)daysLeft} gün içinde sona erecek";
                TxtLicenseExpiry.Text  = $"Bitiş: {license.ExpiryDate:dd MMMM yyyy}";
            }
            else
            {
                PanelLicenseInfo.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                TxtLicenseIcon.Text    = "✅";
                TxtLicenseStatus.Text  = "Lisans Aktif";
                TxtLicenseExpiry.Text  = $"Bitiş: {license.ExpiryDate:dd MMMM yyyy} ({(int)daysLeft} gün kaldı)";
            }
            TxtLicensePackage.Text = license.PackageType == "multi" ? "Paket: Çok Ekran" : "Paket: Tek Ekran";
#endif
        }

        private void LoadIconPreview()
        {
            var iconPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EsnafPos", "custom_icon.png");
            if (!System.IO.File.Exists(iconPath)) return;
            try
            {
                var bitmap = new BitmapImage(new Uri(iconPath));
                ImgIconPreview.Source = bitmap;
                TxtIconPath.Text = System.IO.Path.GetFileName(iconPath);
            }
            catch { }
        }

        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Ikon Sec",
                Filter = "Resim Dosyaları|*.png;*.jpg;*.jpeg;*.ico|Tüm Dosyalar|*.*"
            };
            if (dialog.ShowDialog() != true) return;
            try
            {
                var iconDir  = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EsnafPos");
                var destPath = System.IO.Path.Combine(iconDir, "custom_icon.png");
                System.IO.Directory.CreateDirectory(iconDir);
                System.IO.File.Copy(dialog.FileName, destPath, overwrite: true);
                var bitmap = new BitmapImage(new Uri(destPath));
                ImgIconPreview.Source = bitmap;
                TxtIconPath.Text = System.IO.Path.GetFileName(dialog.FileName);
                var window = Window.GetWindow(this);
                if (window != null) window.Icon = bitmap;
                ShowToast("Ikon guncellendi! Bir sonraki acilista da gecerli olacak.");
            }
            catch (Exception ex)
            {
                ShowToast($"Ikon yuklenemedi: {ex.Message}", success: false);
            }
        }

        private void BtnSaveBusinessSettings_Click(object sender, RoutedEventArgs e)
        {
            _settings.Business.AppName      = TxtAppName.Text.Trim();
            _settings.Business.BusinessName = TxtBusinessName.Text.Trim();
            _settings.Business.Address      = TxtBusinessAddress.Text.Trim();
            _settings.Business.Phone        = TxtBusinessPhone.Text.Trim();
            _settings.Business.ReceiptNote  = TxtReceiptNote.Text.Trim();
            _settings.Save();
            ShowToast("Isletme ayarlari kaydedildi! Uygulamayi yeniden baslatın.");
        }

        // ─── AĞ AYARLARI ──────────────────────────────────────────

        private void LoadNetworkSettings()
        {
            var net = _settings.Network;
            RbStandalone.IsChecked  = net.Mode == AppMode.Standalone;
            RbServer.IsChecked      = net.Mode == AppMode.Server;
            RbClient.IsChecked      = net.Mode == AppMode.Client;
            TxtServerPort.Text      = net.ServerPort.ToString();
            TxtServerIp.Text        = net.ServerIp;
            TxtApiUsername.Text     = net.ApiUsername;
            TxtApiPassword.Password = net.ApiPassword;
            UpdateNetworkPanels();
        }

        private void AppMode_Changed(object sender, RoutedEventArgs e)
            => UpdateNetworkPanels();

        private void UpdateNetworkPanels()
        {
            if (PanelClientSettings == null) return;
            bool isClient = RbClient?.IsChecked == true;
            bool isServer = RbServer?.IsChecked == true;
            PanelClientSettings.Visibility = isClient ? Visibility.Visible : Visibility.Collapsed;
            PanelTestConnection.Visibility  = isClient ? Visibility.Visible : Visibility.Collapsed;
            PanelServerInfo.Visibility      = isServer ? Visibility.Visible : Visibility.Collapsed;
            if (isServer) PopulateServerInfo();
        }

        // Sunucu modunda: istemciye yazilacak bilgisayar adi + LAN IP'lerini goster
        private void PopulateServerInfo()
        {
            try
            {
                TxtServerName.Text = Environment.MachineName;
                var ips = System.Net.Dns.GetHostAddresses(Environment.MachineName)
                    .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    .Select(a => a.ToString())
                    .ToList();
                TxtServerIps.Text = ips.Count > 0 ? "IP: " + string.Join(", ", ips) : "";
            }
            catch { }
        }

        private void BtnSaveNetwork_Click(object sender, RoutedEventArgs e)
        {
            var net = _settings.Network;
            if (RbServer.IsChecked == true)       net.Mode = AppMode.Server;
            else if (RbClient.IsChecked == true)  net.Mode = AppMode.Client;
            else                                   net.Mode = AppMode.Standalone;
            if (int.TryParse(TxtServerPort.Text, out var port)) net.ServerPort = port;
            net.ServerIp    = TxtServerIp.Text.Trim();
            net.ApiUsername = TxtApiUsername.Text.Trim();
            net.ApiPassword = TxtApiPassword.Password;
            _settings.Save();
            ShowToast("Ag ayarlari kaydedildi. Uygulamayi yeniden baslatın.");
        }

        private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
        {
            TxtConnectionStatus.Text       = "Test ediliyor...";
            TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(127, 140, 141));
            var client = new ApiClient(
                TxtServerIp.Text.Trim(),
                int.TryParse(TxtServerPort.Text, out var p) ? p : 5150,
                TxtApiUsername.Text.Trim(),
                TxtApiPassword.Password);
            bool ok = await client.TestConnectionAsync();
            TxtConnectionStatus.Text       = ok ? "✓ Baglandi!" : "✕ Baglanamadi";
            TxtConnectionStatus.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));
        }

        // ─── ENTER TUŞU KISAYOLLARI ───────────────────────────────

        private void TxtEnter_AddTable(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Enter) BtnAddTable_Click(sender, e); }

        private void TxtEnter_AddCategory(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Enter) BtnAddCategory_Click(sender, e); }

        private void TxtEnter_AddUser(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Enter) BtnAddUser_Click(sender, e); }

        private void TxtEnter_AddChannel(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Enter) BtnAddChannel_Click(sender, e); }

        private void TxtEnter_SaveChannel(object sender, System.Windows.Input.KeyEventArgs e)
        { if (e.Key == System.Windows.Input.Key.Enter) BtnSaveEditChannel_Click(sender, e); }

        // ─── KANAL YÖNETİMİ ──────────────────────────────────────

        private async void BtnAddChannel_Click(object sender, RoutedEventArgs e)
        {
            await _vm.AddChannelCommand.ExecuteAsync(null);
            ShowToast("Kanal eklendi");
        }

        private void BtnStartEditChannel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppChannel ch)
                _vm.StartEditChannelCommand.Execute(ch);
        }

        private async void BtnSaveEditChannel_Click(object sender, RoutedEventArgs e)
        {
            await _vm.SaveEditChannelCommand.ExecuteAsync(null);
            ShowToast("Kanal guncellendi");
        }

        private async void BtnDeleteChannel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not AppChannel ch) return;
            var r = MessageBox.Show($"'{ch.Name}' kanalı silinecek. Bu kanaldaki kategoriler etkilenmez.",
                "Sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
            await _vm.DeleteChannelCommand.ExecuteAsync(ch);
            ShowToast("Kanal silindi");
        }

        private async void BtnMoveChannelUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppChannel ch)
                await _vm.MoveChannelUpCommand.ExecuteAsync(ch);
        }

        private async void BtnMoveChannelDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppChannel ch)
                await _vm.MoveChannelDownCommand.ExecuteAsync(ch);
        }

        // ─── YAZICI ───────────────────────────────────────────────

        private void LoadPrinterSettings()
        {
            var p = _settings.Printer;
            RbUsb.IsChecked        = p.ConnectionType == "USB";
            RbLan.IsChecked        = p.ConnectionType == "LAN";
            TxtUsbPrinterName.Text = p.UsbPrinterName;
            TxtLanIp.Text          = p.LanIp;
            TxtLanPort.Text        = p.LanPort.ToString();
            UpdatePrinterPanels();
        }

        private void PrinterType_Changed(object sender, RoutedEventArgs e)
            => UpdatePrinterPanels();

        private void UpdatePrinterPanels()
        {
            if (PanelUsb == null) return;
            bool isLan = RbLan?.IsChecked == true;
            PanelUsb.Visibility = isLan ? Visibility.Collapsed : Visibility.Visible;
            PanelLan.Visibility = isLan ? Visibility.Visible   : Visibility.Collapsed;
        }

        private void BtnSavePrinter_Click(object sender, RoutedEventArgs e)
        {
            _settings.Printer.ConnectionType = RbLan.IsChecked == true ? "LAN" : "USB";
            _settings.Printer.UsbPrinterName = TxtUsbPrinterName.Text.Trim();
            _settings.Printer.LanIp          = TxtLanIp.Text.Trim();
            if (int.TryParse(TxtLanPort.Text, out var lp))
                _settings.Printer.LanPort = lp;
            _settings.Save();
            ShowToast("Yazici ayarlari kaydedildi");
        }

        private async void BtnTestPrint_Click(object sender, RoutedEventArgs e)
        {
            await _vm.TestPrintCommand.ExecuteAsync(null);
            ShowToast("Test baskisi gonderildi");
        }
    }
}
