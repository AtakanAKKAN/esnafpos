using ClosedXML.Excel;
using EsnafPos.ViewModels;
using Microsoft.Win32;

namespace EsnafPos.Services
{
    public class ExcelExportService
    {
        public void ExportDailyReport(ReportsViewModel vm)
        {
            var path = GetSavePath($"Gunluk_Rapor_{vm.SelectedDate:yyyy-MM-dd}.xlsx");
            if (path == null) return;

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Günlük Rapor");

            SetTitle(ws, $"Günlük Rapor - {vm.SelectedDate:dd.MM.yyyy}", "A1:C1");

            ws.Cell("A3").Value = "Toplam Ciro";
            ws.Cell("B3").Value = vm.DailyTotal;
            ws.Cell("A4").Value = "Nakit";
            ws.Cell("B4").Value = vm.DailyCash;
            ws.Cell("A5").Value = "Kart";
            ws.Cell("B5").Value = vm.DailyCard;
            ws.Cell("A6").Value = "Sipariş Sayısı";
            ws.Cell("B6").Value = vm.DailyOrderCount;
            StyleSummary(ws, 3, 6);

            ws.Cell("A8").Value = "Ürün Bazlı Satışlar";
            ws.Cell("A8").Style.Font.Bold = true;
            ws.Cell("A8").Style.Font.FontSize = 13;

            SetHeaders(ws, 9, "Ürün Adı", "Porsiyon", "Adet", "Toplam (TL)");

            int row = 10;
            foreach (var item in vm.DailyProductSales)
            {
                ws.Cell(row, 1).Value = item.ProductName;
                ws.Cell(row, 2).Value = item.Portion;
                ws.Cell(row, 3).Value = item.TotalQuantity;
                ws.Cell(row, 4).Value = item.TotalRevenue;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        public void ExportWeeklyReport(ReportsViewModel vm)
        {
            var path = GetSavePath($"Haftalik_Rapor_{vm.WeekStartDate:yyyy-MM-dd}.xlsx");
            if (path == null) return;

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Haftalık Rapor");

            SetTitle(ws, $"Haftalık Rapor - {vm.WeekStartDate:dd.MM.yyyy} / {vm.WeekEndDate:dd.MM.yyyy}", "A1:C1");

            ws.Cell("A3").Value = "Toplam Ciro";
            ws.Cell("B3").Value = vm.WeeklyTotal;
            ws.Cell("A4").Value = "Nakit";
            ws.Cell("B4").Value = vm.WeeklyCash;
            ws.Cell("A5").Value = "Kart";
            ws.Cell("B5").Value = vm.WeeklyCard;
            ws.Cell("A6").Value = "Sipariş Sayısı";
            ws.Cell("B6").Value = vm.WeeklyOrderCount;
            StyleSummary(ws, 3, 6);

            ws.Cell("A8").Value = "Günlük Detay";
            ws.Cell("A8").Style.Font.Bold = true;

            SetHeaders(ws, 9, "Tarih", "Sipariş Sayısı", "Ciro (TL)");

            int row = 10;
            foreach (var item in vm.WeeklyDetails)
            {
                ws.Cell(row, 1).Value = item.DayDate;
                ws.Cell(row, 2).Value = item.OrderCount;
                ws.Cell(row, 3).Value = item.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        public void ExportMonthlyReport(ReportsViewModel vm)
        {
            var monthNames = new[] { "", "Ocak", "Subat", "Mart", "Nisan", "Mayis",
                "Haziran", "Temmuz", "Agustos", "Eylul", "Ekim", "Kasim", "Aralik" };
            var monthName = monthNames[vm.SelectedMonth];

            var path = GetSavePath($"Aylik_Rapor_{vm.SelectedYear}_{monthName}.xlsx");
            if (path == null) return;

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Aylık Rapor");

            SetTitle(ws, $"Aylık Rapor - {monthName} {vm.SelectedYear}", "A1:C1");

            ws.Cell("A3").Value = "Toplam Ciro";
            ws.Cell("B3").Value = vm.MonthlyTotal;
            ws.Cell("A4").Value = "Nakit";
            ws.Cell("B4").Value = vm.MonthlyCash;
            ws.Cell("A5").Value = "Kart";
            ws.Cell("B5").Value = vm.MonthlyCard;
            ws.Cell("A6").Value = "Sipariş Sayısı";
            ws.Cell("B6").Value = vm.MonthlyOrderCount;
            StyleSummary(ws, 3, 6);

            ws.Cell("A8").Value = "Günlük Detay";
            ws.Cell("A8").Style.Font.Bold = true;

            SetHeaders(ws, 9, "Tarih", "Sipariş Sayısı", "Ciro (TL)");

            int row = 10;
            foreach (var item in vm.MonthlyDetails)
            {
                ws.Cell(row, 1).Value = item.DayDate;
                ws.Cell(row, 2).Value = item.OrderCount;
                ws.Cell(row, 3).Value = item.TotalRevenue;
                ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            ws.Columns().AdjustToContents();
            wb.SaveAs(path);
            OpenFile(path);
        }

        private static void SetTitle(IXLWorksheet ws, string title, string mergeRange)
        {
            ws.Cell("A1").Value = title;
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Cell("A1").Style.Font.FontName = "Arial";
            ws.Range(mergeRange).Merge();
        }

        private static void SetHeaders(IXLWorksheet ws, int row, params string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontName = "Arial";
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C3E50");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        private static void StyleSummary(IXLWorksheet ws, int startRow, int endRow)
        {
            for (int r = startRow; r <= endRow; r++)
            {
                ws.Cell(r, 1).Style.Font.Bold = true;
                ws.Cell(r, 1).Style.Font.FontName = "Arial";
                ws.Cell(r, 2).Style.Font.FontName = "Arial";
                if (r < endRow)
                    ws.Cell(r, 2).Style.NumberFormat.Format = "#,##0.00 TL";
            }
        }

        private static string? GetSavePath(string defaultName)
        {
            var dlg = new SaveFileDialog
            {
                FileName = defaultName,
                DefaultExt = ".xlsx",
                Filter = "Excel Dosyasi|*.xlsx"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        private static void OpenFile(string path)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}
