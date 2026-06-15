using ESCPOS_NET.Emitters;
using ESCPOS_NET.Utilities;
using EsnafPos.Models;
using EsnafPos.Services;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace EsnafPos.Services
{
    public class PrinterService
    {
        private readonly SettingsService _settings;

        private static readonly byte[] SET_CODEPAGE_TURKISH = { 0x1B, 0x74, 0x12 };
        private static readonly Encoding TurkishEncoding = GetTurkishEncoding();

        private static Encoding GetTurkishEncoding()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1254);
        }

        public PrinterService(SettingsService settings)
        {
            _settings = settings;
        }

        // ─── ODEME FISI (tam kesim) ───────────────────────────
        public async Task<bool> PrintReceipt(Order order, List<OrderItem> items)
        {
            try
            {
                var commands = BuildCommands(order, items, isCheck: false);
                await SendToPrinter(commands);
                return true;
            }
            catch (Exception ex)
            {
                ShowPrinterError(ex.Message);
                return false;
            }
        }

        // ─── ADiSYON / CHECK (yari kesim) ────────────────────
        public async Task<bool> PrintCheck(Order order, List<OrderItem> items)
        {
            try
            {
                var commands = BuildCommands(order, items, isCheck: true);
                await SendToPrinter(commands);
                return true;
            }
            catch (Exception ex)
            {
                ShowPrinterError(ex.Message);
                return false;
            }
        }

        // ─── FIS YAPISI ──────────────────────────────────────
        private byte[] BuildCommands(Order order, List<OrderItem> items, bool isCheck)
        {
            var e = new EPSON();
            const int W = 32;

            // Baslik
            var cmd = ByteSplicer.Combine(
                e.Initialize(),
                SET_CODEPAGE_TURKISH,
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleWidth),
                Enc("Corbaci Ciko\n"),
                e.SetStyles(PrintStyle.None)
            );

            if (isCheck)
            {
                // Adisyon basliginda "ADiSYON" yazisi
                cmd = ByteSplicer.Combine(cmd,
                    e.SetStyles(PrintStyle.Bold),
                    Enc("- ADiSYON -\n"),
                    e.SetStyles(PrintStyle.None)
                );
            }

            cmd = ByteSplicer.Combine(cmd,
                Enc(DateTime.Now.ToString("dd.MM.yyyy  HH:mm") + "\n"),
                Enc("\n"),
                e.SetStyles(PrintStyle.Bold),
                Enc($"Masa: {order.TableNameSnapshot}\n"),
                e.SetStyles(PrintStyle.None),
                Enc("================================\n"),
                e.LeftAlign()
            );

            // Urunler - adisyonda her kalem daha genis
            if (isCheck)
            {
                foreach (var item in items)
                {
                    var name = item.Portion == "Tam"
                        ? item.NameSnapshot
                        : $"{item.NameSnapshot} ({item.Portion})";

                    // Urun adi (tam satir genisligi)
                    cmd = ByteSplicer.Combine(cmd,
                        Enc("\n"),
                        e.SetStyles(PrintStyle.Bold),
                        Enc($"  {name}\n"),
                        e.SetStyles(PrintStyle.None),
                        Enc($"  {item.Quantity} adet x {item.PriceSnapshot:N2} TL\n"),
                        Enc(FormatLine("  Ara toplam:",
                            $"{item.LineTotal:N2} TL", W) + "\n")
                    );
                }
            }
            else
            {
                foreach (var item in items)
                {
                    var left = item.Portion == "Tam"
                        ? $"{item.Quantity}x {item.NameSnapshot}"
                        : $"{item.Quantity}x {item.NameSnapshot} ({item.Portion})";
                    cmd = ByteSplicer.Combine(cmd,
                        Enc(FormatLine(left, $"{item.LineTotal:N2} TL", W) + "\n"));
                }
            }

            // Alt kisim
            cmd = ByteSplicer.Combine(cmd,
                e.CenterAlign(),
                Enc("================================\n"),
                e.LeftAlign(),
                Enc("\n"),
                e.SetStyles(PrintStyle.Bold),
                Enc(FormatLine("TOPLAM", $"{order.TotalAmount:N2} TL", W) + "\n"),
                e.SetStyles(PrintStyle.None),
                Enc(FormatLine("Odeme:", GetPaymentTypeName(order.PaymentType), W) + "\n"),
                Enc("\n"),
                Enc("\n"),
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold),
                Enc("Afiyet olsun, yine bekleriz!\n"),
                e.SetStyles(PrintStyle.None),
                Enc("\n"),
                Enc("\n"),
                Enc("\n"),
                Enc("\n"),
                Enc("\n")
            );

            cmd = ByteSplicer.Combine(cmd,
                isCheck ? e.PartialCut() : e.FullCut());

            return cmd;
        }

        // ─── YARDIMCI ────────────────────────────────────────

        private static byte[] Enc(string text) => TurkishEncoding.GetBytes(text);

        private static string FormatLine(string left, string right, int totalWidth)
        {
            var spaces = totalWidth - left.Length - right.Length;
            if (spaces < 1) spaces = 1;
            return left + new string(' ', spaces) + right;
        }

        private static string GetPaymentTypeName(PaymentType? type) => type switch
        {
            PaymentType.Cash => "Nakit",
            PaymentType.CardDebit => "Banka Karti",
            PaymentType.CardCredit => "Kredi Karti",
            _ => "-"
        };

        private static void ShowPrinterError(string msg)
        {
            System.Windows.MessageBox.Show(
                $"Yazıcı hatası: {msg}",
                "Yazıcı Hatası",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }

        // ─── GONDERIM ────────────────────────────────────────

        private async Task SendToPrinter(byte[] commands)
        {
            var p = _settings.Printer;

            if (p.ConnectionType == "LAN" && !string.IsNullOrEmpty(p.LanIp))
            {
                using var client = new TcpClient();
                await client.ConnectAsync(p.LanIp, p.LanPort);
                using var stream = client.GetStream();
                await stream.WriteAsync(commands);
                await stream.FlushAsync();
                return;
            }

            if (p.ConnectionType == "USB" && !string.IsNullOrEmpty(p.UsbPrinterName))
            {
                await Task.Run(() => RawPrint(p.UsbPrinterName, commands));
                return;
            }

            throw new Exception(
                "Yazıcı ayarlanmamış.\nAdmin paneli → Yazıcı Ayarları bölümünden ayarlayınız.");
        }

        private static void RawPrint(string printerName, byte[] data)
        {
            var di = new DOCINFOA { pDocName = "ESC/POS", pDataType = "RAW" };

            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
                throw new Exception($"Yazıcı açılamadı: {printerName}");

            try
            {
                if (!StartDocPrinter(hPrinter, 1, di)) throw new Exception("StartDocPrinter başarısız.");
                if (!StartPagePrinter(hPrinter)) throw new Exception("StartPagePrinter başarısız.");
                if (!WritePrinter(hPrinter, data, data.Length, out _)) throw new Exception("WritePrinter başarısız.");
                EndPagePrinter(hPrinter);
                EndDocPrinter(hPrinter);
            }
            finally { ClosePrinter(hPrinter); }
        }

        #region Windows Print API

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "";
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool OpenPrinter(string szPrinter, out IntPtr hPrinter, IntPtr pd);
        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level,
            [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);
        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

        #endregion
    }
}
