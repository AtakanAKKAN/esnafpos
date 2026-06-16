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

        // ─── GUN SONU (Z) OZETI ──────────────────────────────
        public async Task<bool> PrintDailySummary(
            DateTime date, decimal cash, decimal card, decimal veresiye, int orderCount,
            IReadOnlyList<(string Name, string Portion, int Qty, decimal Revenue)> products)
        {
            try
            {
                await SendToPrinter(BuildDailySummary(date, cash, card, veresiye, orderCount, products));
                return true;
            }
            catch (Exception ex)
            {
                ShowPrinterError(ex.Message);
                return false;
            }
        }

        private byte[] BuildDailySummary(
            DateTime date, decimal cash, decimal card, decimal veresiye, int orderCount,
            IReadOnlyList<(string Name, string Portion, int Qty, decimal Revenue)> products)
        {
            var e = new EPSON();
            const int W = 32;
            var biz = _settings.Business;
            string header = !string.IsNullOrWhiteSpace(biz.BusinessName) ? biz.BusinessName
                          : !string.IsNullOrWhiteSpace(biz.AppName)      ? biz.AppName
                          : "Esnaf POS";

            var cmd = ByteSplicer.Combine(
                e.Initialize(),
                SET_CODEPAGE_TURKISH,
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleWidth),
                Enc(header + "\n"),
                e.SetStyles(PrintStyle.Bold),
                Enc("- GÜN SONU -\n"),
                e.SetStyles(PrintStyle.None),
                Enc(date.ToString("dd.MM.yyyy") + "\n"),
                Enc("================================\n"),
                e.LeftAlign(),
                Enc(FormatLine("Nakit:", $"{cash:N2} TL", W) + "\n"),
                Enc(FormatLine("Kart:",  $"{card:N2} TL", W) + "\n"),
                e.SetStyles(PrintStyle.Bold),
                Enc(FormatLine("TOPLAM:", $"{(cash + card):N2} TL", W) + "\n"),
                e.SetStyles(PrintStyle.None),
                Enc(FormatLine("Veresiye:", $"{veresiye:N2} TL", W) + "\n"),
                Enc(FormatLine("Satış adedi:", orderCount.ToString(), W) + "\n"),
                Enc("================================\n")
            );

            if (products.Count > 0)
            {
                cmd = ByteSplicer.Combine(cmd,
                    e.SetStyles(PrintStyle.Bold),
                    Enc("Ürün Bazlı Satışlar\n"),
                    e.SetStyles(PrintStyle.None));
                foreach (var p in products)
                {
                    var name = p.Portion == "Tam" ? p.Name : $"{p.Name} ({p.Portion})";
                    cmd = ByteSplicer.Combine(cmd,
                        Enc(FormatLine($"{p.Qty}x {name}", $"{p.Revenue:N2} TL", W) + "\n"));
                }
                cmd = ByteSplicer.Combine(cmd, Enc("================================\n"));
            }

            cmd = ByteSplicer.Combine(cmd,
                e.CenterAlign(),
                Enc(DateTime.Now.ToString("dd.MM.yyyy HH:mm") + " yazdırıldı\n"),
                Enc("\n"), Enc("\n"), Enc("\n"), Enc("\n"),
                e.FullCut());

            return cmd;
        }

        // ─── FIS YAPISI ──────────────────────────────────────
        private byte[] BuildCommands(Order order, List<OrderItem> items, bool isCheck)
        {
            var e = new EPSON();
            const int W = 32;
            var biz = _settings.Business;
            string header = !string.IsNullOrWhiteSpace(biz.BusinessName) ? biz.BusinessName
                          : !string.IsNullOrWhiteSpace(biz.AppName)      ? biz.AppName
                          : "Esnaf POS";

            // Baslik — isletme adi (Admin → Isletme Ayarlari'ndan)
            var cmd = ByteSplicer.Combine(
                e.Initialize(),
                SET_CODEPAGE_TURKISH,
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold | PrintStyle.DoubleWidth),
                Enc(header + "\n"),
                e.SetStyles(PrintStyle.None)
            );

            // Adres / telefon (doluysa)
            if (!string.IsNullOrWhiteSpace(biz.Address))
                cmd = ByteSplicer.Combine(cmd, Enc(biz.Address + "\n"));
            if (!string.IsNullOrWhiteSpace(biz.Phone))
                cmd = ByteSplicer.Combine(cmd, Enc("Tel: " + biz.Phone + "\n"));

            if (isCheck)
            {
                // Adisyon basliginda "ADiSYON" yazisi
                cmd = ByteSplicer.Combine(cmd,
                    e.SetStyles(PrintStyle.Bold),
                    Enc("- ADİSYON -\n"),
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
                Enc(FormatLine("Ödeme:", GetPaymentTypeName(order.PaymentType), W) + "\n"),
                Enc("\n"),
                Enc("\n"),
                e.CenterAlign(),
                e.SetStyles(PrintStyle.Bold),
                Enc((string.IsNullOrWhiteSpace(biz.ReceiptNote)
                        ? "Afiyet olsun, yine bekleriz!"
                        : biz.ReceiptNote) + "\n"),
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
            PaymentType.CardDebit => "Banka Kartı",
            PaymentType.CardCredit => "Kredi Kartı",
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
