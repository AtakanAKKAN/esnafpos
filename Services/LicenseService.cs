using EsnafPos.Models;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Net.Http;

namespace EsnafPos.Services
{
    public enum LicenseStatus
    {
        Valid,
        Expired,
        NotActivated,
        Invalid
    }

    public class LicenseService
    {
        private const string SignatureSecret = "EsnafPos_2024_LicenseKey";

#if DEBUG
        public bool IsDebugMode => true;
        public LicenseStatus Status => LicenseStatus.Valid;
        public string PackageType   => "multi";
        public DateTime ExpiryDate  => DateTime.MaxValue;
#else
        public bool IsDebugMode => false;

        private LicenseStatus _status = LicenseStatus.NotActivated;
        public  LicenseStatus Status  => _status;

        private string   _packageType = "single";
        public  string   PackageType  => _packageType;

        private DateTime _expiryDate  = DateTime.MinValue;
        public  DateTime ExpiryDate   => _expiryDate;
#endif

        private static string LicensePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EsnafPos", "license.json");

        // FIX-6: Yedek dosya yolu — ana dosya bozulursa buradan kurtarılır
        private static string LicenseBackupPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EsnafPos", "license.backup.json");

        private static readonly string _licenseApiUrl =
            "https://esnaflisans.onrender.com";

        private CancellationTokenSource? _periodicCts = null;

        // ─── FIX-1: Kalan gün hesabı — UTC/yerel saat dilimi güvenli ─
        private static int CalcRemaining(LicenseCache cache)
        {
            // .ToLocalTime() ile saat dilimi farkından kaynaklanan erken
            // "Expired" sorununu önlüyoruz (örn. UTC+3'te gece 01:00)
            var expiryLocal = cache.ExpiryDate.ToLocalTime().Date;
            return (expiryLocal - DateTime.Today).Days;
            // Not: 0 = son gün, hala geçerli. Sadece < 0 expired sayılır.
        }

        // ─── STARTUP: KALAN GÜN TAMPERİNG KONTROLÜ ───────────────────
        public LicenseStatus CheckOnStartup()
        {
#if DEBUG
            return LicenseStatus.Valid;
#else
            var cache = LoadCache();
            if (cache == null) return LicenseStatus.NotActivated;

            if (!VerifySignature(cache)) return LicenseStatus.Invalid;

            var current = CalcRemaining(cache);

            if (current < 0) return LicenseStatus.Expired;

            // Tarih manipülasyonu kontrolü.
            // PC hiç kapanmadan çalışırken 24h kontroller LastCheckedRemaining'i
            // düşürmüş olabilir, CloseRemainingDays eski/yüksek kalabilir.
            // İkisinden küçük olanı (en kısıtlayıcı) eşik olarak kullan.
            var threshold = cache.CloseRemainingDays;
            if (cache.LastCheckedRemaining != 9999 && cache.LastCheckedRemaining < threshold)
                threshold = cache.LastCheckedRemaining;

            if (current > threshold)
                return LicenseStatus.Invalid;

            // İlk aktivasyondan sonra henüz hiç kapanmadıysa (9999 default)
            if (cache.CloseRemainingDays == 9999)
            {
                cache.CloseRemainingDays   = current;
                cache.LastCheckedRemaining = current;
                cache.LastCheckedTime      = DateTime.Now;
                cache.Signature = ComputeSignature(cache);
                WriteCache(cache);
            }

            return LicenseStatus.Valid;
#endif
        }

        // ─── KAPANIŞTA ÇAĞRI ──────────────────────────────────────────
        public void SaveOnClose()
        {
#if DEBUG
            return;
#else
            var cache = LoadCache();
            if (cache == null) return;

            cache.CloseRemainingDays = CalcRemaining(cache);
            cache.Signature = ComputeSignature(cache);
            WriteCache(cache);
#endif
        }

        // ─── 24 SAATLİK PERİYODİK KONTROL ───────────────────────────
        public void StartPeriodicCheck(Action<LicenseStatus> onTampered)
        {
#if DEBUG
            return;
#else
            _periodicCts = new CancellationTokenSource();
            var token = _periodicCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromHours(24), token)
                              .ContinueWith(_ => { });
                    if (token.IsCancellationRequested) break;

                    // FIX: Bir turdaki geçici hata (dosya kilidi, IO) tüm periyodik
                    // kontrolü sessizce öldürmesin — try/catch ile turu atla, döngü
                    // devam etsin. Lisansı sırf geçici bir hata yüzünden engelleme.
                    try
                    {
                        var cache = LoadCache();
                        if (cache == null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(
                                () => onTampered(LicenseStatus.Invalid));
                            break;
                        }

                        var current = CalcRemaining(cache);

                        if (current > cache.LastCheckedRemaining)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(
                                () => onTampered(LicenseStatus.Invalid));
                            break;
                        }

                        if (current < 0)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(
                                () => onTampered(LicenseStatus.Expired));
                            break;
                        }

                        cache.LastCheckedRemaining = current;
                        cache.LastCheckedTime      = DateTime.Now;
                        cache.Signature = ComputeSignature(cache);
                        WriteCache(cache);

                        _ = TryRefreshFromServerAsync();
                    }
                    catch (Exception)
                    {
                        // Geçici hata — bu turu atla, bir sonraki 24s döngüsünde tekrar dene.
                    }
                }
            }, token);
#endif
        }

        public void StopPeriodicCheck() => _periodicCts?.Cancel();

        // ─── BAŞLANGIÇ KONTROLÜ (arka planda, sunucu) ─────────────────
        public async Task CheckLicenseInBackgroundAsync(Action<LicenseStatus> onResult)
        {
#if DEBUG
            onResult(LicenseStatus.Valid);
            return;
#else
            await Task.Run(async () =>
            {
                var status = await CheckLicenseAsync();
                _status = status;
                System.Windows.Application.Current?.Dispatcher.Invoke(
                    () => onResult(status));
            });
#endif
        }

        // ─── AKTİVASYON ──────────────────────────────────────────────
        public async Task<(bool Success, string Error)> ActivateAsync(string activationCode)
        {
            try
            {
                var token      = GetOrCreateMachineToken();
                var macAddress = GetMacAddress();

                var payload = new { activationCode, machineToken = token, macAddress };

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var json = JsonSerializer.Serialize(payload);
                var res  = await http.PostAsync(
                    $"{_licenseApiUrl}/api/license/activate",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    return (false, ParseError(err));
                }

                var body    = await res.Content.ReadAsStringAsync();
                var license = JsonSerializer.Deserialize<LicenseResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (license == null) return (false, "Sunucudan geçersiz yanıt.");

                SaveCache(token, license);
#if !DEBUG
                _status      = LicenseStatus.Valid;
                _packageType = license.PackageType ?? "single";
                _expiryDate  = license.ExpiryDate;
#endif
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Bağlantı hatası: {ex.Message}");
            }
        }

        // ─── LİSANS KONTROLÜ (sunucu) ────────────────────────────────
        private async Task<LicenseStatus> CheckLicenseAsync()
        {
            var cache = LoadCache();
            if (cache == null) return LicenseStatus.NotActivated;

            if (!VerifySignature(cache)) return LicenseStatus.Invalid;

            if (DateTime.Now < cache.ServerDate.ToLocalTime().AddDays(-1))
                return LicenseStatus.Invalid;

            if (DateTime.Now > cache.ExpiryDate.ToLocalTime()) // FIX-1
                return await VerifyWithServerAsync(cache.MachineToken);

            _ = Task.Run(async () =>
            {
                var serverStatus = await VerifyWithServerAsync(cache.MachineToken);
                if (serverStatus == LicenseStatus.Invalid ||
                    serverStatus == LicenseStatus.Expired)
                    DeleteCache();
            });

#if !DEBUG
            _packageType = cache.PackageType;
            _expiryDate  = cache.ExpiryDate;
#endif
            return LicenseStatus.Valid;
        }

        private async Task<LicenseStatus> VerifyWithServerAsync(string machineToken)
        {
            try
            {
                var payload = new { machineToken, macAddress = GetMacAddress() };

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var json = JsonSerializer.Serialize(payload);
                var res  = await http.PostAsync(
                    $"{_licenseApiUrl}/api/license/verify",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode) return LicenseStatus.Expired;

                var body    = await res.Content.ReadAsStringAsync();
                var license = JsonSerializer.Deserialize<LicenseResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (license == null || !license.IsValid) return LicenseStatus.Expired;

                SaveCache(machineToken, license);
#if !DEBUG
                _packageType = license.PackageType ?? "single";
                _expiryDate  = license.ExpiryDate;
#endif
                return LicenseStatus.Valid;
            }
            catch
            {
                return LicenseStatus.Valid;
            }
        }

        private async Task TryRefreshFromServerAsync()
        {
            var cache = LoadCache();
            if (cache == null) return;
            await VerifyWithServerAsync(cache.MachineToken);
        }

        // ─── MOD DEĞİŞİKLİĞİ DOĞRULAMASI ────────────────────────────
        public async Task<(bool Success, string Error)> ValidateModeChangeAsync(string activationCode)
        {
#if DEBUG
            return (true, "");
#else
            try
            {
                var token      = GetOrCreateMachineToken();
                var macAddress = GetMacAddress();

                var payload = new { activationCode, machineToken = token, macAddress };

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var json = JsonSerializer.Serialize(payload);
                var res  = await http.PostAsync(
                    $"{_licenseApiUrl}/api/license/mode-change",
                    new StringContent(json, Encoding.UTF8, "application/json"));

                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    return (false, ParseError(err));
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Bağlantı hatası: {ex.Message}");
            }
#endif
        }

        // ─── YARDIMCI METODLAR ────────────────────────────────────────
        public bool HasValidCache() => LoadCache() != null;

        public int GetRemainingDays()
        {
            var cache = LoadCache();
            return cache == null ? 0 : CalcRemaining(cache);
        }

        private static string GetOrCreateMachineToken()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EsnafPos", "machine.token");

            if (File.Exists(path)) return File.ReadAllText(path).Trim();

            var token = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, token);
            return token;
        }

        private static string GetMacAddress()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(n => n.GetPhysicalAddress().ToString())
                    .FirstOrDefault(m => !string.IsNullOrEmpty(m)) ?? "UNKNOWN";
            }
            catch { return "UNKNOWN"; }
        }

        private static void SaveCache(string token, LicenseResponse license)
        {
            var existing  = LoadCache();
            var remaining = (license.ExpiryDate.ToLocalTime().Date - DateTime.Today).Days; // FIX-1

            // FIX-4: Lisans yenilendi mi? (yeni remaining > mevcut CloseRemainingDays)
            // Yenilendiyse kalan gün alanlarını da yeni değerle güncelle.
            // Bu kontrol sunucudan gelen yanıtta yapıldığı için güvenli.
            bool isRenewal = existing != null && remaining > existing.CloseRemainingDays;

            var cache = new LicenseCache
            {
                MachineToken = token,
                ExpiryDate   = license.ExpiryDate,
                ServerDate   = license.ServerDate,
                PackageType  = license.PackageType ?? "single",
                BusinessName = license.BusinessName ?? "",

                CloseRemainingDays   = isRenewal ? remaining : (existing?.CloseRemainingDays   ?? remaining),
                LastCheckedRemaining = isRenewal ? remaining : (existing?.LastCheckedRemaining ?? remaining),
                LastCheckedTime      = isRenewal ? DateTime.Now : (existing?.LastCheckedTime   ?? DateTime.Now),
            };
            cache.Signature = ComputeSignature(cache);
            WriteCache(cache);
        }

        private static void WriteCache(LicenseCache cache)
        {
            var json = JsonSerializer.Serialize(cache,
                new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
            File.WriteAllText(LicensePath, json);

            // FIX-6: Ana dosyayı yazdıktan hemen sonra yedeği güncelle
            try { File.Copy(LicensePath, LicenseBackupPath, overwrite: true); } catch { }
        }

        private static LicenseCache? LoadCache()
        {
            // FIX-6: Ana dosyayı dene, bozuksa yedekten kurtar
            var cache = TryReadCacheFile(LicensePath);
            if (cache != null) return cache;

            // Ana dosya bozuk veya yok — yedekten kurtar
            cache = TryReadCacheFile(LicenseBackupPath);
            if (cache != null)
            {
                // Yedekten ana dosyayı yenile
                try { File.Copy(LicenseBackupPath, LicensePath, overwrite: true); } catch { }
            }

            return cache;
        }

        private static LicenseCache? TryReadCacheFile(string path)
        {
            if (!File.Exists(path)) return null;

            // FIX: Dosya geçici olarak kilitliyse (antivirüs, yedekleme veya
            // eşzamanlı yazma) tek seferlik okuma hatasını "dosya yok/bozuk"
            // sanıp lisansı geçersiz saymayalım — kısa aralıklarla birkaç kez
            // dene. Sadece kalıcı hata / bozuk JSON null döndürür.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<LicenseCache>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (IOException)
                {
                    // Geçici kilit olabilir — kısa bekle ve tekrar dene
                    Thread.Sleep(150);
                }
                catch
                {
                    // JSON bozuk / başka kalıcı hata — tekrar denemenin anlamı yok
                    return null;
                }
            }
            return null;
        }

        private static void DeleteCache()
        {
            try { if (File.Exists(LicensePath))       File.Delete(LicensePath);       } catch { }
            try { if (File.Exists(LicenseBackupPath)) File.Delete(LicenseBackupPath); } catch { }
        }

        // ─── İMZA ────────────────────────────────────────────────────
        private static string ComputeSignature(LicenseCache cache)
        {
            var raw = $"{cache.MachineToken}|{cache.ExpiryDate:O}|{cache.PackageType}" +
                      $"|{cache.CloseRemainingDays}|{cache.LastCheckedRemaining}|{SignatureSecret}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLower();
        }

        private static string ComputeSignatureLegacy(LicenseCache cache)
        {
            // Güncelleme öncesi eski format — migrasyon için korunuyor
            var raw   = $"{cache.MachineToken}|{cache.ExpiryDate:O}|{cache.PackageType}|{SignatureSecret}";
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLower();
        }

        private static bool VerifySignature(LicenseCache cache)
        {
            // Yeni imzayı dene
            if (cache.Signature == ComputeSignature(cache)) return true;

            // Eski (legacy) imzayı dene — güncelleme migrasyonu
            if (cache.Signature == ComputeSignatureLegacy(cache))
            {
                var remaining = CalcRemaining(cache);
                cache.CloseRemainingDays   = remaining;
                cache.LastCheckedRemaining = remaining;
                cache.LastCheckedTime      = DateTime.Now;
                cache.Signature = ComputeSignature(cache);
                WriteCache(cache);
                return true;
            }

            return false;
        }

        private static string ParseError(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var e))
                    return e.GetString() ?? "Bilinmeyen hata.";
            }
            catch { }
            return "Sunucudan hata alındı.";
        }
    }

    // ─── SUNUCU YANIT MODELİ ─────────────────────────────────────────
    public class LicenseResponse
    {
        public bool     IsValid      { get; set; }
        public DateTime ExpiryDate   { get; set; }
        public string?  PackageType  { get; set; }
        public string?  BusinessName { get; set; }
        public DateTime ServerDate   { get; set; }
    }
}
