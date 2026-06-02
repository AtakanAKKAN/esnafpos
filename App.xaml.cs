using EsnafPos.Data;
using EsnafPos.Network;
using EsnafPos.Services;
using EsnafPos.ViewModels;
using EsnafPos.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace EsnafPos
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        public static ApiServer?       Server       { get; private set; }
        public static ApiClient?       Client       { get; private set; }
        private static CancellationTokenSource _discoveryCts = new();
        public static LicenseService   License  { get; private set; } = new();

        private static Mutex? _mutex;

        private static readonly string _logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EsnafPos", "startup.log");

        private static void Log(string msg)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath)!);

                // 3 günden eski log dosyasını sil
                if (System.IO.File.Exists(_logPath))
                {
                    var lastWrite = System.IO.File.GetLastWriteTime(_logPath);
                    if ((DateTime.Now - lastWrite).TotalDays > 3)
                        System.IO.File.Delete(_logPath);
                }

                System.IO.File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss} {msg}\n");
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        protected override async void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                Log($"UNHANDLED: {ex.ExceptionObject}");
                MessageBox.Show(ex.ExceptionObject.ToString(), "Kritik Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            Log("=== Uygulama başlatıldı ===");
            base.OnStartup(e);

            // Tek instance kontrolu
            _mutex = new Mutex(true, "EsnafPos_SingleInstance", out bool isNewInstance);
            if (!isNewInstance)
            {
                Log("Zaten çalışıyor, kapatılıyor.");
                var current = System.Diagnostics.Process.GetCurrentProcess();
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                {
                    if (proc.Id == current.Id) continue;
                    var hwnd = proc.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) continue;
                    if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                    break;
                }
                Shutdown();
                return;
            }

            // ─── LİSANS KONTROLÜ ─────────────────────────────────────
#if !DEBUG
            try
            {
                var tempSettings = new SettingsService();
                Log($"Ağ modu: {tempSettings.Network.Mode}");

                if (tempSettings.Network.Mode != AppMode.Client)
                {
                    var hasCache = License.HasValidCache();
                    Log($"HasValidCache: {hasCache}");

                    if (!hasCache)
                    {
                        Log("Cache yok, aktivasyon ekranı açılıyor.");
                        var activationWindow = new ActivationWindow(License);
                        var activated = activationWindow.ShowDialog();
                        if (activated != true)
                        {
                            Log("Aktivasyon iptal edildi.");
                            Shutdown();
                            return;
                        }
                        Log("Aktivasyon başarılı.");
                    }

                    var startupStatus = License.CheckOnStartup();
                    Log($"CheckOnStartup: {startupStatus}");

                    if (startupStatus == LicenseStatus.Invalid)
                    {
                        MessageBox.Show(
                            "Sistem tarihi hatalı görünüyor.\nLütfen tarih ve saat ayarlarınızı kontrol edin.",
                            "Lisans Hatası",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        Shutdown();
                        return;
                    }
                    if (startupStatus == LicenseStatus.Expired)
                    {
                        MessageBox.Show(
                            "Lisansınızın süresi dolmuştur.\nLütfen satıcınızla iletişime geçin.",
                            "Lisans Süresi Doldu",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        Shutdown();
                        return;
                    }

                    // Sunucu kontrolü arka planda
                    Task.Run(async () =>
                    {
                        await License.CheckLicenseInBackgroundAsync(status =>
                        {
                            Log($"Arka plan lisans kontrolü: {status}");
                            if (status == LicenseStatus.Expired || status == LicenseStatus.Invalid)
                            {
                                MessageBox.Show(
                                    "Lisansınızın süresi dolmuştur.\nLütfen satıcınızla iletişime geçin.",
                                    "Lisans Süresi Doldu",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                                Shutdown();
                            }
                        });
                    });

                    // PC açık kalırsa 24 saatlik periyodik kontrol
                    License.StartPeriodicCheck(status =>
                    {
                        Log($"Periyodik kontrol tetiklendi: {status}");
                        var msg = status == LicenseStatus.Expired
                            ? "Lisansınızın süresi dolmuştur.\nLütfen satıcınızla iletişime geçin."
                            : "Sistem tarihi hatalı görünüyor.\nLütfen tarih ve saat ayarlarınızı kontrol edin.";
                        MessageBox.Show(msg, "Lisans Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    });
                }
            }
            catch (Exception ex)
            {
                Log($"LİSANS EXCEPTION: {ex}");
                MessageBox.Show($"Lisans kontrolünde hata:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
#endif

            // ─── SERVİSLER ───────────────────────────────────────────
            try
            {
                Log("Servisler kuruluyor...");
                var services = new ServiceCollection();
                ConfigureServices(services);
                Services = services.BuildServiceProvider();
                Log("Servisler hazır.");

                var settings = Services.GetRequiredService<SettingsService>();
                if (settings.Network.Mode != AppMode.Client)
                {
                    Log("Veritabanı başlatılıyor...");
                    using var scope = Services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // Ensure metodları önce — yeni tablolar seed'den önce oluşmalı
                    db.EnsureCustomerNameColumn();
                    db.EnsureChannelsTable();
                    db.EnsureCategoryChannelColumn();
                    db.EnsureCollectedQuantityColumn();
                    db.EnsureOrderChangeLogTable();
                    db.EnsureLastItemAddedAtColumn();
                    db.EnsureVeresiyeQuantityColumn();
                    DatabaseInitializer.Initialize(db); // ← en sona
                    Log("Veritabanı hazır.");
                }

                var net = settings.Network;

                if (net.Mode == AppMode.Server)
                {
                    var dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "EsnafPos", "esnafpos.db");
                    Server = new ApiServer(net.ApiUsername, net.ApiPassword, dbPath);
                    _ = Task.Run(async () =>
                    {
                        try { await Server.StartAsync(net.ServerPort); }
                        catch (Exception ex)
                        {
                            Log($"Sunucu hatası: {ex.Message}");
                            var logPath = System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                "EsnafPos", "server_error.log");
                            await System.IO.File.WriteAllTextAsync(logPath,
                                $"{DateTime.Now}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                            Current.Dispatcher.Invoke(() =>
                                MessageBox.Show($"API Sunucu baslanamadi:\n{ex.Message}",
                                    "Sunucu Hatasi", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    });

                    // Discovery listener — istemciler otomatik bulsun
                    _ = NetworkDiscovery.StartListenerAsync(net.ServerPort, _discoveryCts.Token);
                }
                else if (net.Mode == AppMode.Client)
                {
                    // Önce ağda sunucuyu otomatik bul
                    var (discoveredIp, discoveredPort) = await NetworkDiscovery.DiscoverServerAsync(timeoutMs: 4000);

                    string serverIp   = discoveredIp   ?? net.ServerIp;
                    int    serverPort = discoveredPort > 0 ? discoveredPort : net.ServerPort;

                    if (discoveredIp != null)
                    {
                        Log($"Sunucu otomatik bulundu: {serverIp}:{serverPort}");
                        // Bulunan IP'yi kaydet — bir sonraki açılış için fallback
                        net.ServerIp   = serverIp;
                        net.ServerPort = serverPort;
                        settings.Save();
                    }
                    else
                    {
                        Log($"Otomatik keşif başarısız, kayıtlı IP kullanılıyor: {serverIp}:{serverPort}");
                    }

                    Client = new ApiClient(serverIp, serverPort,
                        net.ApiUsername, net.ApiPassword);
                }

                StartBackupService();

                Log("Ana pencere açılıyor...");
                var mainWindow = Services.GetRequiredService<MainWindow>();

                var iconPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EsnafPos", "custom_icon.png");
                if (System.IO.File.Exists(iconPath))
                {
                    try { mainWindow.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath)); }
                    catch { }
                }

                mainWindow.Show();
                Log("Ana pencere açıldı.");

                // ─── GÜNCELLEME KONTROLÜ ─────────────────────────────
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var updateService = Services.GetRequiredService<UpdateService>();
                        var update = await updateService.CheckForUpdateAsync();
                        Log($"Update kontrolü: {(update == null ? "güncelleme yok" : $"v{update.VersionNumber} IsForced={update.IsForced}")}");

                        if (update == null) return;

                        Current.Dispatcher.Invoke(() =>
                        {
                            var updateWindow = new UpdateWindow(updateService, update);
                            updateWindow.ShowDialog();

                            // HasUpdate VE IsForced ikisi birden true olmalı
                            if (update.HasUpdate && update.IsForced)
                            {
                                Log("Zorunlu güncelleme, kapatılıyor.");
                                Shutdown();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Log($"Update EXCEPTION: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"STARTUP EXCEPTION: {ex}");
                MessageBox.Show($"Başlatma hatası:\n{ex.Message}", "Hata",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        // ─── KAPANIŞ ─────────────────────────────────────────────────
        protected override void OnExit(ExitEventArgs e)
        {
            Log("Uygulama kapanıyor...");
            License.StopPeriodicCheck();
            License.SaveOnClose();
            Log("Kapanış tamamlandı.");
            base.OnExit(e);
        }

        private static void StartBackupService()
        {
            Task.Run(async () =>
            {
                try
                {
                    var dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "EsnafPos", "esnafpos.db");
                    var backupDir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "EsnafPos", "Yedekler");
                    System.IO.Directory.CreateDirectory(backupDir);
                    var todayFile = System.IO.Path.Combine(
                        backupDir, $"esnafpos_{DateTime.Today:yyyy-MM-dd}.db");
                    if (!System.IO.File.Exists(todayFile) && System.IO.File.Exists(dbPath))
                        System.IO.File.Copy(dbPath, todayFile, overwrite: true);
                    var cutoff = DateTime.Today.AddDays(-30);
                    foreach (var file in System.IO.Directory.GetFiles(backupDir, "esnafpos_*.db"))
                    {
                        var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                        if (fileName.Length >= 18 &&
                            DateTime.TryParse(fileName.Substring(9), out var fileDate) &&
                            fileDate < cutoff)
                            System.IO.File.Delete(file);
                    }
                }
                catch { }
            });
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddDbContext<AppDbContext>();

            services.AddSingleton<SessionService>();
            services.AddSingleton<PrinterService>();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<ExcelExportService>();
            services.AddSingleton(License);
            services.AddSingleton<UpdateService>();

            services.AddTransient<LoginViewModel>();
            services.AddTransient<TablesViewModel>();
            services.AddTransient<OrderViewModel>();
            services.AddTransient<PaymentViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<AdminViewModel>();
            services.AddTransient<AdminPage>();
            services.AddTransient<ReportsPage>();

            services.AddTransient<MainWindow>();
        }
    }
}
