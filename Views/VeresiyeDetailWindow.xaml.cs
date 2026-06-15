using CommunityToolkit.Mvvm.ComponentModel;
using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public partial class VeresiyeItemRow : ObservableObject
    {
        public int     OrderItemId       { get; set; }
        public int     OrderId           { get; set; }
        public int     PaymentId         { get; set; }
        public string  DisplayName       { get; set; } = "";
        public decimal UnitPrice         { get; set; }
        public int     RemainingQuantity { get; set; }  // VeresiyeQuantity - henuz tahsil edilmemis

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RowTotal))]
        private int _selectedQuantity;

        public decimal RowTotal => SelectedQuantity * UnitPrice;

        public void Increase()
        {
            if (SelectedQuantity < RemainingQuantity) SelectedQuantity++;
        }

        public void Decrease()
        {
            if (SelectedQuantity > 0) SelectedQuantity--;
        }
    }

    public partial class VeresiyeDetailWindow : Window
    {
        private readonly AppDbContext     _db;
        private readonly VeresiyeCardItem _card;
        private readonly ObservableCollection<VeresiyeItemRow> _rows = new();
        private List<Payment> _payments = new();

        public bool AnyCollected { get; private set; } = false;

        public VeresiyeDetailWindow(AppDbContext db, VeresiyeCardItem card)
        {
            InitializeComponent();
            _db   = db;
            _card = card;

            TxtInitial.Text      = card.Initial;
            TxtCustomerName.Text = card.CustomerName;

            IcItems.ItemsSource = _rows;
            Loaded += async (s, e) => await LoadItems();
        }

        private async Task LoadItems()
        {
            // Bu musteri icin veresiye odeme kayitlarini bul
            _payments = await _db.Payments
                .Where(p => p.PaymentType == PaymentType.Veresiye && p.CustomerName != null)
                .ToListAsync();

            _payments = _payments
                .Where(p => string.Equals(p.CustomerName, _card.CustomerName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            var orderIds = _payments.Select(p => p.OrderId).Distinct().ToList();

            // Sadece VeresiyeQuantity > 0 olan urunleri goster
            // (CollectedQuantity kullanilmiyor - veresiye takibi VeresiyeQuantity ile yapiliyor)
            var items = await _db.OrderItems
                .Where(oi => orderIds.Contains(oi.OrderId) && oi.VeresiyeQuantity > 0)
                .OrderBy(oi => oi.OrderId).ThenBy(oi => oi.Id)
                .ToListAsync();

            _rows.Clear();

            foreach (var item in items)
            {
                var payment = _payments.FirstOrDefault(p => p.OrderId == item.OrderId);
                if (payment == null) continue;

                var row = new VeresiyeItemRow
                {
                    OrderItemId       = item.Id,
                    OrderId           = item.OrderId,
                    PaymentId         = payment.Id,
                    DisplayName       = item.Portion == "Tam"
                        ? item.NameSnapshot
                        : $"{item.NameSnapshot} ({item.Portion})",
                    UnitPrice         = item.PriceSnapshot,
                    RemainingQuantity = item.VeresiyeQuantity,
                    SelectedQuantity  = 0
                };

                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(VeresiyeItemRow.RowTotal))
                        RecalcTotal();
                };

                _rows.Add(row);
            }

            var totalDebt = _payments.Sum(p => p.Amount);
            TxtTotalDebt.Text = $"Toplam Borc: {totalDebt:N2} TL";
            RecalcTotal();
        }

        private void RecalcTotal()
        {
            TxtSelectedTotal.Text = $"{_rows.Sum(r => r.RowTotal):N2} TL";
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows)
                row.SelectedQuantity = row.RemainingQuantity;
        }

        private void BtnIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VeresiyeItemRow row)
                row.Increase();
        }

        private void BtnDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VeresiyeItemRow row)
                row.Decrease();
        }

        private async void BtnTahsilEt_Click(object sender, RoutedEventArgs e)
        {
            var selectedRows = _rows.Where(r => r.SelectedQuantity > 0).ToList();
            if (!selectedRows.Any())
            {
                MessageBox.Show("Lütfen tahsil edilecek ürün seçin!",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedTotal = selectedRows.Sum(r => r.RowTotal);
            var result = MessageBox.Show(
                $"{_card.CustomerName} — {selectedTotal:N2} TL tahsil edildi mi?",
                "Tahsil Onayı", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            // VeresiyeQuantity azalt, CollectedQuantity artir
            foreach (var row in selectedRows)
            {
                var item = await _db.OrderItems.FindAsync(row.OrderItemId);
                if (item != null)
                {
                    item.VeresiyeQuantity  -= row.SelectedQuantity;
                    item.CollectedQuantity += row.SelectedQuantity;
                    if (item.VeresiyeQuantity < 0) item.VeresiyeQuantity = 0;
                    _db.OrderItems.Update(item);
                }
            }

            // Her siparis icin: veresiye tamamen tahsil edildiyse odemeyi kapat
            var affectedOrderIds = selectedRows.Select(r => r.OrderId).Distinct().ToList();

            foreach (var orderId in affectedOrderIds)
            {
                var orderItems = await _db.OrderItems
                    .Where(oi => oi.OrderId == orderId)
                    .ToListAsync();

                // Bu siparise ait TUM veresiye odemeler (birden fazla olabilir)
                var orderPayments = _payments.Where(p => p.OrderId == orderId).ToList();

                // Bu siparisin veresiyesi tamamen tahsil edildi mi?
                // SaveChangesAsync oncesinde VeresiyeQuantity in-memory guncellendi,
                // bu yuzden EF tracked entity uzerinden kontrol et.
                bool allVeresiyeCollected = orderItems.All(oi => oi.VeresiyeQuantity <= 0);

                var collectedAmount = selectedRows
                    .Where(r => r.OrderId == orderId)
                    .Sum(r => r.RowTotal);

                if (allVeresiyeCollected)
                {
                    // Tam tahsil: Bu siparise ait TUM veresiye kayitlarini Cash'e cevir
                    foreach (var payment in orderPayments)
                    {
                        payment.PaymentType = PaymentType.Cash;
                        _db.Payments.Update(payment);
                    }

                    var order = await _db.Orders.FindAsync(orderId);
                    if (order != null && order.Status == OrderStatus.Veresiye)
                        order.Status = OrderStatus.Paid;
                }
                else
                {
                    // Kismi tahsil: toplam veresiye tutarindan dusur
                    // Birden fazla payment varsa sirayla azalt
                    var remaining = collectedAmount;
                    foreach (var payment in orderPayments)
                    {
                        if (remaining <= 0) break;
                        var deduct = Math.Min(payment.Amount, remaining);
                        payment.Amount -= deduct;
                        payment.CreatedAt = DateTime.Now; // sayac sifirla
                        remaining -= deduct;
                        if (payment.Amount < 0) payment.Amount = 0;
                        _db.Payments.Update(payment);
                    }

                    // Tahsil edilen kisim icin Cash odeme ekle
                    _db.Payments.Add(new Payment
                    {
                        OrderId      = orderId,
                        Amount       = collectedAmount,
                        PaymentType  = PaymentType.Cash,
                        CustomerName = null,
                        CreatedAt    = DateTime.Now
                    });
                }
            }

            await _db.SaveChangesAsync();
            AnyCollected = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
