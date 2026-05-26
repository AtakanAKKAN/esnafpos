using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EsnafPos.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly AppDbContext _db;

        // GUNLUK
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
        [ObservableProperty] private decimal _dailyCash;
        [ObservableProperty] private decimal _dailyCard;
        [ObservableProperty] private decimal _dailyTotal;
        [ObservableProperty] private int     _dailyOrderCount;
        [ObservableProperty] private decimal _dailyVeresiye;
        [ObservableProperty] private bool    _dailyHasVeresiye;

        // HAFTALIK
        [ObservableProperty] private DateTime _weekStartDate;
        [ObservableProperty] private DateTime _weekEndDate;
        [ObservableProperty] private string   _weekLabel = "";
        [ObservableProperty] private decimal  _weeklyTotal;
        [ObservableProperty] private decimal  _weeklyCash;
        [ObservableProperty] private decimal  _weeklyCard;
        [ObservableProperty] private int      _weeklyOrderCount;
        [ObservableProperty] private decimal  _weeklyVeresiye;
        [ObservableProperty] private bool     _weeklyHasVeresiye;

        // AYLIK
        [ObservableProperty] private int     _selectedMonth = DateTime.Today.Month;
        [ObservableProperty] private int     _selectedYear  = DateTime.Today.Year;
        [ObservableProperty] private decimal _monthlyTotal;
        [ObservableProperty] private decimal _monthlyCash;
        [ObservableProperty] private decimal _monthlyCard;
        [ObservableProperty] private int     _monthlyOrderCount;
        [ObservableProperty] private decimal _monthlyVeresiye;
        [ObservableProperty] private bool    _monthlyHasVeresiye;

        public ObservableCollection<ProductSaleItem> DailyProductSales  { get; } = new();
        public ObservableCollection<ProductSaleItem> WeeklyProductSales  { get; } = new();
        public ObservableCollection<ProductSaleItem> MonthlyProductSales { get; } = new();
        public ObservableCollection<DailyTotalItem>  WeeklyDetails       { get; } = new();
        public ObservableCollection<DailyTotalItem>  MonthlyDetails      { get; } = new();
        public ObservableCollection<EsnafPos.Models.OrderChangeLog> ChangeLogs { get; } = new();

        public ReportsViewModel(AppDbContext db)
        {
            _db = db;
            SetWeek(GetMondayOfWeek(DateTime.Today));
        }

        // ─── HAFTA NAViGASYONU ────────────────────────────────
        private static DateTime GetMondayOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
        }

        private void SetWeek(DateTime monday)
        {
            WeekStartDate = monday;
            WeekEndDate   = monday.AddDays(6);
            WeekLabel     = $"{WeekStartDate:d MMM} - {WeekEndDate:d MMM yyyy}";
        }

        [RelayCommand] private async Task PreviousWeek() { SetWeek(WeekStartDate.AddDays(-7)); await LoadWeeklyReport(); }
        [RelayCommand] private async Task NextWeek()     { SetWeek(WeekStartDate.AddDays(7));  await LoadWeeklyReport(); }

        // ─── GUNLUK ──────────────────────────────────────────
        [RelayCommand]
        public async Task LoadDailyReport()
        {
            IsBusy = true;
            try
            {
                var dateStr = SelectedDate.ToString("yyyy-MM-dd");

                // Tum siparisler (Paid + Open kismi odeme)
                var orders = await _db.Orders
                    .Where(o => o.DayDate == dateStr
                             && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Open
                                 || o.Status == OrderStatus.Veresiye))
                    .ToListAsync();

                var orderIds = orders.Select(o => o.Id).ToList();
                DailyOrderCount = orders.Count(o => o.Status == OrderStatus.Paid);

                // Odeme bazli hesapla
                var payments = await _db.Payments
                    .Where(p => orderIds.Contains(p.OrderId)).ToListAsync();

                DailyCash = payments.Where(p => p.PaymentType == PaymentType.Cash).Sum(p => p.Amount);
                DailyCard = payments.Where(p => p.PaymentType == PaymentType.CardDebit
                                           || p.PaymentType == PaymentType.CardCredit).Sum(p => p.Amount);
                DailyTotal = DailyCash + DailyCard;

                var dailyVPay = payments.Where(p => p.PaymentType == PaymentType.Veresiye).ToList();
                DailyVeresiye = dailyVPay.Sum(p => p.Amount);
                DailyHasVeresiye = DailyVeresiye > 0;

                await LoadProductSales(dateStr, dateStr, DailyProductSales);
                await LoadChangeLogs(dateStr, dateStr);
            }
            finally { IsBusy = false; }
        }

        // ─── HAFTALIK ─────────────────────────────────────────
        [RelayCommand]
        public async Task LoadWeeklyReport()
        {
            IsBusy = true;
            try
            {
                var startStr = WeekStartDate.ToString("yyyy-MM-dd");
                var endStr   = WeekEndDate.ToString("yyyy-MM-dd");

                var orders = await _db.Orders
                    .Where(o => string.Compare(o.DayDate, startStr) >= 0
                             && string.Compare(o.DayDate, endStr) <= 0
                             && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Open
                                 || o.Status == OrderStatus.Veresiye))
                    .ToListAsync();

                var orderIds = orders.Select(o => o.Id).ToList();
                WeeklyOrderCount = orders.Count(o => o.Status == OrderStatus.Paid);

                var payments = await _db.Payments
                    .Where(p => orderIds.Contains(p.OrderId)).ToListAsync();

                WeeklyCash = payments.Where(p => p.PaymentType == PaymentType.Cash).Sum(p => p.Amount);
                WeeklyCard = payments.Where(p => p.PaymentType == PaymentType.CardDebit
                                           || p.PaymentType == PaymentType.CardCredit).Sum(p => p.Amount);
                WeeklyTotal = WeeklyCash + WeeklyCard;

                WeeklyVeresiye = payments.Where(p => p.PaymentType == PaymentType.Veresiye).Sum(p => p.Amount);
                WeeklyHasVeresiye = WeeklyVeresiye > 0;

                var details = orders
                    .GroupBy(o => o.DayDate)
                    .Select(g => new DailyTotalItem
                    {
                        DayDate = g.Key,
                        TotalRevenue = g.Sum(o => o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(x => x.DayDate).ToList();

                WeeklyDetails.Clear();
                foreach (var d in details) WeeklyDetails.Add(d);

                await LoadProductSales(startStr, endStr, WeeklyProductSales);
            }
            finally { IsBusy = false; }
        }

        // ─── AYLIK ────────────────────────────────────────────
        [RelayCommand]
        public async Task LoadMonthlyReport()
        {
            IsBusy = true;
            try
            {
                var monthPattern = $"{SelectedYear}-{SelectedMonth:D2}";

                var orders = await _db.Orders
                    .Where(o => o.DayDate.StartsWith(monthPattern)
                             && (o.Status == OrderStatus.Paid || o.Status == OrderStatus.Open
                                 || o.Status == OrderStatus.Veresiye))
                    .ToListAsync();

                var orderIds = orders.Select(o => o.Id).ToList();
                MonthlyOrderCount = orders.Count(o => o.Status == OrderStatus.Paid);

                var payments = await _db.Payments
                    .Where(p => orderIds.Contains(p.OrderId)).ToListAsync();

                MonthlyCash = payments.Where(p => p.PaymentType == PaymentType.Cash).Sum(p => p.Amount);
                MonthlyCard = payments.Where(p => p.PaymentType == PaymentType.CardDebit
                                            || p.PaymentType == PaymentType.CardCredit).Sum(p => p.Amount);
                MonthlyTotal = MonthlyCash + MonthlyCard;

                MonthlyVeresiye = payments.Where(p => p.PaymentType == PaymentType.Veresiye).Sum(p => p.Amount);
                MonthlyHasVeresiye = MonthlyVeresiye > 0;

                var details = orders
                    .GroupBy(o => o.DayDate)
                    .Select(g => new DailyTotalItem
                    {
                        DayDate = g.Key,
                        TotalRevenue = g.Sum(o => o.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .OrderBy(x => x.DayDate).ToList();

                MonthlyDetails.Clear();
                foreach (var d in details) MonthlyDetails.Add(d);

                var startStr = $"{SelectedYear}-{SelectedMonth:D2}-01";
                var endStr   = $"{SelectedYear}-{SelectedMonth:D2}-31";
                await LoadProductSales(startStr, endStr, MonthlyProductSales);
            }
            finally { IsBusy = false; }
        }

        // ─── DEGISIM LOGLARI ─────────────────────────────────
        public async Task LoadChangeLogs(string startDate, string endDate)
        {
            var logs = await _db.OrderChangeLogs
                .Where(l => string.Compare(l.DayDate, startDate) >= 0
                         && string.Compare(l.DayDate, endDate) <= 0)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            ChangeLogs.Clear();
            foreach (var log in logs) ChangeLogs.Add(log);
        }

        // ─── URUN BAZLI SATISLAR (PORSIYONLU) ────────────────
        private async Task LoadProductSales(string startDate, string endDate,
                                            ObservableCollection<ProductSaleItem> target)
        {
            var rawItems = await _db.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => string.Compare(oi.Order!.DayDate, startDate) >= 0
                          && string.Compare(oi.Order!.DayDate, endDate) <= 0
                          && (oi.Order.Status == OrderStatus.Paid || oi.Order.Status == OrderStatus.Open
                                  || oi.Order.Status == OrderStatus.Veresiye)
                          ) // Tum items (eski kayitlar CollectedQuantity = 0 olabilir)
                .ToListAsync();

            var grouped = rawItems
                .GroupBy(oi => new { oi.ProductId, oi.NameSnapshot, oi.Portion })
                .Select(g => new ProductSaleItem
                {
                    ProductName   = g.Key.NameSnapshot,
                    Portion       = g.Key.Portion,
                    // Yeni sistem: CollectedQuantity veya VeresiyeQuantity kullan
                    // Eski sistem: CollectedQuantity = 0 ise Quantity kullan (eski odemeler)
                    TotalQuantity = g.Sum(x =>
                        (x.CollectedQuantity + x.VeresiyeQuantity) > 0
                            ? x.CollectedQuantity + x.VeresiyeQuantity
                            : x.Quantity),
                    TotalRevenue  = g.Sum(x =>
                        (x.CollectedQuantity + x.VeresiyeQuantity) > 0
                            ? x.PriceSnapshot * (x.CollectedQuantity + x.VeresiyeQuantity)
                            : x.PriceSnapshot * x.Quantity)
                })
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.Portion)
                .ToList();

            target.Clear();
            foreach (var item in grouped) target.Add(item);
        }
    }

    public class ProductSaleItem
    {
        public string  ProductName   { get; set; } = "";
        public string  Portion       { get; set; } = "Tam";
        public int     TotalQuantity { get; set; }
        public decimal TotalRevenue  { get; set; }
    }

    public class DailyTotalItem
    {
        public string  DayDate      { get; set; } = "";
        public decimal TotalRevenue { get; set; }
        public int     OrderCount   { get; set; }
    }
}
