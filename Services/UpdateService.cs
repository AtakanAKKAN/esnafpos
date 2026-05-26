using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace EsnafPos.Services
{
    public class UpdateCheckResult
    {
        public bool    HasUpdate     { get; set; }
        public string? VersionNumber { get; set; }
        public string? DownloadUrl   { get; set; }
        public string? ReleaseNotes  { get; set; }
        public bool    IsForced      { get; set; }
    }

    public class UpdateService
    {
        public const string CurrentVersion = "2.0.0";

        private const string ApiUrl =
            "https://esnaflisans.onrender.com"; // Deploy sonrası güncellenecek

        public async Task<UpdateCheckResult?> CheckForUpdateAsync()
        {
#if DEBUG
            return null; // Debug modda güncelleme kontrolü yok
#else
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var res = await http.GetAsync(
                    $"{ApiUrl}/api/update/check?currentVersion={CurrentVersion}");

                if (!res.IsSuccessStatusCode) return null;

                var json   = await res.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UpdateCheckResult>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result?.HasUpdate == true ? result : null;
            }
            catch { return null; }
#endif
        }

        public async Task<bool> DownloadAndInstallAsync(
            string downloadUrl,
            IProgress<int>? progress = null)
        {
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "EsnafPosUpdate.exe");

                using var http   = new HttpClient();
                using var res    = await http.GetAsync(downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                var total        = res.Content.Headers.ContentLength ?? -1;
                var downloaded   = 0L;

                using var fs     = new FileStream(tempPath, FileMode.Create);
                using var stream = await res.Content.ReadAsStreamAsync();

                var buffer = new byte[8192];
                int read;
                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (total > 0)
                        progress?.Report((int)(downloaded * 100 / total));
                }

                // Mevcut exe'nin yolunu al
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";

                // Güncelleme script'i yaz — eski exe'yi yenisiyle değiştirir
                var scriptPath = Path.Combine(Path.GetTempPath(), "esnafpos_update.bat");
                await File.WriteAllTextAsync(scriptPath, $@"
@echo off
timeout /t 2 /nobreak > nul
copy /y ""{tempPath}"" ""{currentExe}""
start """" ""{currentExe}""
del ""{scriptPath}""
");
                // Mevcut uygulamayı kapat, script çalışsın
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "cmd.exe",
                    Arguments       = $"/c \"{scriptPath}\"",
                    CreateNoWindow  = true,
                    UseShellExecute = false
                });

                System.Windows.Application.Current.Shutdown();
                return true;
            }
            catch { return false; }
        }
    }
}
