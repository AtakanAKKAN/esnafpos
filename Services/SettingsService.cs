using EsnafPos.Network;
using System.IO;
using System.Text.Json;

namespace EsnafPos.Services
{
    public class PrinterSettings
    {
        public string ConnectionType { get; set; } = "USB"; // USB veya LAN
        public string UsbPrinterName { get; set; } = "";
        public string LanIp { get; set; } = "";
        public int LanPort { get; set; } = 9100;
    }

    public class SettingsService
    {
        private readonly string _settingsPath;
        public PrinterSettings Printer { get; private set; } = new();
        public NetworkSettings Network  { get; private set; } = new();
        public BusinessSettings Business { get; private set; } = new();

        public SettingsService()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EsnafPos");
            Directory.CreateDirectory(appData);
            _settingsPath = Path.Combine(appData, "settings.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings?.Printer != null)
                        Printer = settings.Printer;
                    if (settings?.Network != null)
                        Network = settings.Network;
                    if (settings?.Business != null)
                        Business = settings.Business;
                }
            }
            catch { }
        }

        public void Save()
        {
            var settings = new AppSettings { Printer = Printer, Network = Network, Business = Business };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }

        private class AppSettings
        {
            public PrinterSettings Printer { get; set; } = new();
            public NetworkSettings Network  { get; set; } = new();
            public BusinessSettings Business { get; set; } = new();
        }
    }
}