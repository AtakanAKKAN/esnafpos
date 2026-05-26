using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public class OrderHistoryItem
    {
        public int OrderId { get; set; }
        public string TableNameSnapshot { get; set; } = "";
        public DateTime? ClosedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public PaymentType? PaymentType { get; set; }
        public string PaymentTypeDisplay { get; set; } = "";
        public string? CustomerName { get; set; }
        public bool IsVeresiye => PaymentType == Models.PaymentType.Veresiye;
        public bool WasVeresiye { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }

    public partial class OrderHistoryWindow : Window
    {
        private readonly AppDbContext _db;
        private readonly PrinterService _printer;

        private const int PageSize = 20;
        private int _currentPage = 1;
        private int _totalCount = 0;
        private DateTime? _filterDate = null;

        public OrderHistoryWindow(AppDbContext db, PrinterService printer)
        {
            InitializeComponent();
            _db = db;
            _printer = printer;
            DpFilter.SelectedDate = DateTime.Today;
            Loaded += async (s, e) => await LoadPage(1);
        }

        // ─── SAYFALAMA ────────────────────────────────────────

        private async Task LoadPage(int page)
        {
            IsBusyOverlay.Visibility = Visibility.Visible;
            try
            {
                _currentPage = page;

                // Temel sorgu
                var query = _db.Orders
                    .Where(o => o.Status == OrderStatus.Paid
                             || o.Status == OrderStatus.Veresiye);

                if (_filterDate.HasValue)
                    query = query.Where(o => o.DayDate == _filterDate.Value.ToString("yyyy-MM-dd"));

                // Toplam kayit sayisi (sadece count - hafif)
                _totalCount = await query.CountAsync();

                // Sadece bu sayfanin kayitlari
                var orders = await query
                    .OrderByDescending(o => o.ClosedAt)
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();

                var orderIds = orders.Select(o => o.Id).ToList();

                // Bu 25 siparis icin items ve payments
                var allItems = await _db.OrderItems
                    .Where(i => orderIds.Contains(i.OrderId))
                    .ToListAsync();

                var allPayments = await _db.Payments
                    .Where(p => orderIds.Contains(p.OrderId))
                    .ToListAsync();

                var historyItems = orders
                    .Select(o => BuildHistoryItem(o, allItems, allPayments))
                    .ToList();

                IcOrders.ItemsSource = historyItems;

                // Sayfalama UI guncelle
                int totalPages = Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
                TxtCount.Text = $"{_totalCount} hesap";
                TxtPageInfo.Text = $"{page} / {totalPages}";
                BtnPrevPage.IsEnabled = page > 1;
                BtnNextPage.IsEnabled = page < totalPages;
                BtnPrevPage.Opacity = page > 1 ? 1.0 : 0.4;
                BtnNextPage.Opacity = page < totalPages ? 1.0 : 0.4;
            }
            finally
            {
                IsBusyOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private static OrderHistoryItem BuildHistoryItem(
            Order o,
            List<OrderItem> allItems,
            List<Payment> allPayments)
        {
            var payments = allPayments.Where(p => p.OrderId == o.Id).ToList();
            var dominantPayment = payments
                .GroupBy(p => p.PaymentType)
                .OrderByDescending(g => g.Sum(p => p.Amount))
                .FirstOrDefault();

            var paymentType = dominantPayment?.Key ?? o.PaymentType;

            var veresiyePayment = payments
                .FirstOrDefault(p => p.PaymentType == Models.PaymentType.Veresiye);
            var collectedVeresiyePayment = payments
                .FirstOrDefault(p => p.PaymentType == Models.PaymentType.Cash
                                  && !string.IsNullOrEmpty(p.CustomerName));

            var customerName = veresiyePayment?.CustomerName
                            ?? collectedVeresiyePayment?.CustomerName;
            bool wasVeresiye = veresiyePayment != null || collectedVeresiyePayment != null;

            string display = paymentType switch
            {
                Models.PaymentType.Cash       => "Nakit",
                Models.PaymentType.CardDebit  => "Banka Karti",
                Models.PaymentType.CardCredit => "Kredi Karti",
                Models.PaymentType.Veresiye   => "Veresiye",
                _ => ""
            };

            if (paymentType == Models.PaymentType.Veresiye && !string.IsNullOrEmpty(customerName))
                display = $"Veresiye — {customerName}";
            else if (wasVeresiye && !string.IsNullOrEmpty(customerName))
                display = $"Tahsil Edildi — {customerName}";

            return new OrderHistoryItem
            {
                OrderId            = o.Id,
                TableNameSnapshot  = o.TableNameSnapshot,
                ClosedAt           = o.ClosedAt,
                TotalAmount        = o.TotalAmount,
                PaymentType        = paymentType,
                PaymentTypeDisplay = display,
                CustomerName       = customerName,
                Items              = allItems.Where(i => i.OrderId == o.Id).ToList(),
                WasVeresiye        = wasVeresiye && paymentType != Models.PaymentType.Veresiye
            };
        }

        // ─── FiLTRE ──────────────────────────────────────────

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (DpFilter.SelectedDate == null) return;
            _filterDate = DpFilter.SelectedDate.Value.Date;
            await LoadPage(1);
        }

        private async void BtnAll_Click(object sender, RoutedEventArgs e)
        {
            _filterDate = null;
            await LoadPage(1);
        }

        // ─── SAYFA NAVIGASYON ─────────────────────────────────

        private async void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
                await LoadPage(_currentPage - 1);
        }

        private async void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling(_totalCount / (double)PageSize);
            if (_currentPage < totalPages)
                await LoadPage(_currentPage + 1);
        }

        // ─── ADISYON YAZDIR ───────────────────────────────────

        private async void BtnPrintCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not OrderHistoryItem item) return;

            var order = new Order
            {
                Id                = item.OrderId,
                TableNameSnapshot = item.TableNameSnapshot,
                TotalAmount       = item.TotalAmount,
                PaymentType       = item.PaymentType,
                ClosedAt          = item.ClosedAt
            };

            btn.IsEnabled = false;
            await _printer.PrintCheck(order, item.Items);
            btn.IsEnabled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
