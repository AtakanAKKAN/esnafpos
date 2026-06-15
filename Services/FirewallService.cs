using System.Diagnostics;
using System.Security.Principal;

namespace EsnafPos.Services
{
    /// <summary>
    /// Sunucu modunda gelen istemci bağlantıları için Windows Güvenlik Duvarı (Firewall)
    /// inbound kuralını garanti eder. Kural yoksa ekler — gerekirse tek seferlik UAC onayıyla.
    /// Kural kalıcı olduğundan sonraki açılışlarda tekrar sorulmaz.
    /// </summary>
    public static class FirewallService
    {
        // Kural adına port'u da koyuyoruz: port değişirse yeni kural eklensin,
        // eski (yanlış port) kural "var" diye atlanmasın.
        private static string RuleNameFor(int port) => $"EsnafPos Server {port}";

        /// <summary>5150/TCP gibi sunucu portu için inbound izin kuralını garanti eder.</summary>
        public static void EnsureServerRule(int port)
        {
            try
            {
                var ruleName = RuleNameFor(port);
                if (RuleExists(ruleName)) return;

                var addArgs =
                    $"advfirewall firewall add rule name=\"{ruleName}\" " +
                    $"dir=in action=allow protocol=TCP localport={port} profile=any";

                var psi = new ProcessStartInfo("netsh", addArgs)
                {
                    CreateNoWindow = true,
                    WindowStyle    = ProcessWindowStyle.Hidden,
                };

                if (IsElevated())
                {
                    // Zaten yönetici yetkisi var — doğrudan ekle, çıktıyı yut
                    psi.UseShellExecute        = false;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError  = true;
                }
                else
                {
                    // UAC ile yükselt — tek onay yeterli, kural kalıcı
                    psi.UseShellExecute = true;
                    psi.Verb            = "runas";
                }

                using var p = Process.Start(psi);
                p?.WaitForExit(8000);
            }
            catch
            {
                // Kural eklenemese de (UAC reddi, yetki yok, netsh yok vb.)
                // uygulamanın çalışmasını engelleme.
            }
        }

        private static bool RuleExists(string ruleName)
        {
            try
            {
                var psi = new ProcessStartInfo("netsh",
                    $"advfirewall firewall show rule name=\"{ruleName}\"")
                {
                    CreateNoWindow         = true,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                // Eşleşme yoksa netsh ExitCode 1 döner; varsa 0 ve çıktıda kural adı görünür.
                return p.ExitCode == 0 && output.Contains(ruleName);
            }
            catch { return false; }
        }

        private static bool IsElevated()
        {
            try
            {
                if (!OperatingSystem.IsWindows()) return false;
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }
}
