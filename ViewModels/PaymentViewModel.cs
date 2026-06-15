using CommunityToolkit.Mvvm.ComponentModel;
using EsnafPos.Network;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using EsnafPos.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EsnafPos.ViewModels
{
    public partial class PaymentItemRow : ObservableObject
    {
        public int OrderItemId { get; set; }
        public string DisplayName { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int MaxQuantity { get; set; }
        public int RemainingQuantity { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RowTotal))]
        private int _selectedQuantity;

        public decimal RowTotal => SelectedQuantity * UnitPrice;

        public void IncreaseSelected()
        {
            if (SelectedQuantity < RemainingQuantity) SelectedQuantity++;
        }

        public void DecreaseSelected()
        {
            if (SelectedQuantity > 0) SelectedQuantity--;
        }
    }

    public partial class PaymentViewModel : BaseViewModel
    {
        private readonly AppDbContext _db;
        private readonly PrinterService _printer;

        [ObservableProperty] private string _tableName = "";
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private decimal _remainingAmount;
        [ObservableProperty] private decimal _selectedItemsTotal;

        private Order? _order;
        private EsnafPos.Models.Table? _table;

        public ObservableCollection<PaymentItemRow> ItemRows { get; } = new();
        public ObservableCollection<Payment> ExistingPayments { get; } = new();
        public event Action? PaymentCompleted;
        public bool WasFullyPaid { get; private set; } = false;

        public PaymentViewModel(AppDbContext db, PrinterService printer)
        {
            _db = db;
            _printer = printer;
        }

        public async Task Load(Order order, EsnafPos.Models.Table table)
        {
            _order = order;
            WasFullyPaid = false;
            _table = table;
            TableName = table.Name;

            List<OrderItem> items;
            if (App.Client != null)
            {
                var dtos = await App.Client.GetOrderItemsAsync(order.Id);
                items = dtos.Select(d => new OrderItem
                {
                    Id                = d.Id,
                    OrderId           = d.OrderId,
                    ProductId         = d.ProductId,
                    NameSnapshot      = d.NameSnapshot,
                    PriceSnapshot     = d.PriceSnapshot,
                    Portion           = d.Portion,
                    Quantity          = d.Quantity,
                    CollectedQuantity = d.CollectedQuantity,
                    VeresiyeQuantity  = d.VeresiyeQuantity
                }).ToList();
            }
            else
            {
                items = await _db.OrderItems
                    .Where(i => i.OrderId == order.Id)
                    .ToListAsync();
            }

            // Kalan urunlerin toplami (odenenler dusuldu)
            TotalAmount     = items.Sum(i => (i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity) * i.PriceSnapshot);
            RemainingAmount = TotalAmount;

            ItemRows.Clear();
            foreach (var item in items)
            {
                var remaining = item.Quantity - item.CollectedQuantity - item.VeresiyeQuantity;
                if (remaining <= 0) continue; // tamamen odendi veya veresiyeye yazildi, listede gosterme

                var row = new PaymentItemRow
                {
                    OrderItemId       = item.Id,
                    DisplayName       = item.Portion == "Tam"
                        ? item.NameSnapshot
                        : $"{item.NameSnapshot} ({item.Portion})",
                    UnitPrice         = item.PriceSnapshot,
                    MaxQuantity       = remaining,
                    RemainingQuantity = remaining,
                    SelectedQuantity  = 0
                };
                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PaymentItemRow.SelectedQuantity)
                        || e.PropertyName == nameof(PaymentItemRow.RowTotal))
                        RecalcSelectedTotal();
                };
                ItemRows.Add(row);
            }

            RecalcSelectedTotal();

            // Daha once yapilan odemeler (sadece gosterim icin)
            List<Payment> existingPayments;
            if (App.Client != null)
            {
                var dtos = await App.Client.GetPaymentsAsync(order.Id);
                existingPayments = dtos.Select(d => new Payment
                {
                    Id          = d.Id,
                    OrderId     = d.OrderId,
                    Amount      = d.Amount,
                    PaymentType = Enum.TryParse<PaymentType>(d.PaymentType, out var pt) ? pt : PaymentType.Cash,
                    CustomerName = d.CustomerName,
                    CreatedAt   = d.CreatedAt
                }).ToList();
            }
            else
            {
                existingPayments = await _db.Payments
                    .Where(p => p.OrderId == order.Id)
                    .ToListAsync();
            }
            ExistingPayments.Clear();
            foreach (var p in existingPayments)
                ExistingPayments.Add(p);

            RemainingAmount = TotalAmount;
        }

        public async Task UndoExistingPayment(Payment payment)
        {
            if (_order == null) return;

            _db.Payments.Remove(payment);

            var items = await _db.OrderItems
                .Where(i => i.OrderId == _order.Id)
                .ToListAsync();
            foreach (var item in items)
            {
                item.CollectedQuantity  = 0;
                item.VeresiyeQuantity   = 0; // veresiye de sifirla
                _db.OrderItems.Update(item);
            }

            _order.Status = OrderStatus.Open;
            _db.Orders.Update(_order);

            if (_table != null)
            {
                _table.Status = TableStatus.Active;
                _db.Tables.Update(_table);
            }

            await _db.SaveChangesAsync();

            ExistingPayments.Remove(payment);
            TotalAmount = items.Sum(i => i.Quantity * i.PriceSnapshot);
            RemainingAmount = TotalAmount;

            ItemRows.Clear();
            foreach (var item in items)
            {
                var row = new PaymentItemRow
                {
                    OrderItemId       = item.Id,
                    DisplayName       = item.Portion == "Tam"
                        ? item.NameSnapshot
                        : $"{item.NameSnapshot} ({item.Portion})",
                    UnitPrice         = item.PriceSnapshot,
                    MaxQuantity       = item.Quantity,
                    RemainingQuantity = item.Quantity,
                    SelectedQuantity  = 0
                };
                row.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PaymentItemRow.SelectedQuantity)
                        || e.PropertyName == nameof(PaymentItemRow.RowTotal))
                        RecalcSelectedTotal();
                };
                ItemRows.Add(row);
            }
            RecalcSelectedTotal();
        }

        public void RecalcSelectedTotal()
        {
            SelectedItemsTotal = ItemRows.Sum(r => r.RowTotal);
        }

        public void ConsumeSelected()
        {
            foreach (var row in ItemRows)
            {
                row.RemainingQuantity -= row.SelectedQuantity;
                row.SelectedQuantity = 0;
            }
            RecalcSelectedTotal();
        }

        public async Task CompletePaymentWithEntries(
            List<PaymentEntry> entries, bool skipPrint)
        {
            if (_order == null || _table == null) return;
            IsBusy = true;
            try
            {
                // İstemci modunda API üzerinden
                if (App.Client != null)
                {
                    var req = new CompletePaymentRequest
                    {
                        OrderId = _order.Id,
                        Payments = entries.Select(e => new PaymentEntryDto
                        {
                            PaymentType  = e.PaymentType,
                            Amount       = e.Amount,
                            CustomerName = e.CustomerName
                        }).ToList(),
                        CashConsumed = entries
                            .Where(e => e.PaymentType != PaymentType.Veresiye)
                            .SelectMany(e => e.ConsumedItems)
                            .GroupBy(c => c.OrderItemId)
                            .Select(g => new ConsumedItemDto
                                { OrderItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                            .ToList(),
                        VeresiyeConsumed = entries
                            .Where(e => e.PaymentType == PaymentType.Veresiye)
                            .SelectMany(e => e.ConsumedItems)
                            .GroupBy(c => c.OrderItemId)
                            .Select(g => new ConsumedItemDto
                                { OrderItemId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                            .ToList()
                    };
                    var result = await App.Client.CompletePaymentAsync(req);
                    WasFullyPaid = result?.WasFullyPaid ?? false;
                    PaymentCompleted?.Invoke();
                    return;
                }
                var dominantType = entries
                    .GroupBy(e => e.PaymentType)
                    .OrderByDescending(g => g.Sum(e => e.Amount))
                    .First().Key;

                bool hasVeresiye = entries.Any(e => e.PaymentType == PaymentType.Veresiye);

                _order.PaymentType = dominantType;

                foreach (var entry in entries)
                {
                    _db.Payments.Add(new Payment
                    {
                        OrderId      = _order.Id,
                        Amount       = entry.Amount,
                        PaymentType  = entry.PaymentType,
                        CustomerName = entry.CustomerName,
                        CreatedAt    = DateTime.Now
                    });
                }

                // Nakit/Kart entryleri -> CollectedQuantity guncelle
                // Veresiye entryleri     -> VeresiyeQuantity guncelle
                var cashConsumed = new Dictionary<int, int>();
                var veresiyeConsumed = new Dictionary<int, int>();

                foreach (var entry in entries)
                {
                    var target = entry.PaymentType == PaymentType.Veresiye
                        ? veresiyeConsumed : cashConsumed;
                    foreach (var c in entry.ConsumedItems)
                    {
                        target.TryGetValue(c.OrderItemId, out var existing);
                        target[c.OrderItemId] = existing + c.Quantity;
                    }
                }

                foreach (var (itemId, qty) in cashConsumed)
                {
                    var item = await _db.OrderItems.FindAsync(itemId);
                    if (item == null) continue;
                    item.CollectedQuantity += qty;
                    _db.OrderItems.Update(item);
                }

                foreach (var (itemId, qty) in veresiyeConsumed)
                {
                    var item = await _db.OrderItems.FindAsync(itemId);
                    if (item == null) continue;
                    item.VeresiyeQuantity += qty;
                    _db.OrderItems.Update(item);
                }

                // allItems: DB'den taze cek (CollectedQuantity/VeresiyeQuantity guncellendi)
                var allItems = await _db.OrderItems
                    .Where(i => i.OrderId == _order.Id)
                    .ToListAsync();

                // Siparisin GERCEK toplami = tum urunlerin tam fiyati
                // TotalAmount (kalan tutar) ile karistirma → gecmis hesaplarda dogru gorunsun
                _order.TotalAmount = allItems.Sum(i => i.Quantity * i.PriceSnapshot);

                // allItemsPaid: CollectedQuantity + VeresiyeQuantity >= Quantity
                bool allItemsPaid = allItems.All(i =>
                    i.CollectedQuantity + i.VeresiyeQuantity >= i.Quantity);

                if (allItemsPaid)
                {
                    _order.Status   = hasVeresiye ? OrderStatus.Veresiye : OrderStatus.Paid;
                    _order.ClosedAt = DateTime.Now;
                    _table.Status   = TableStatus.Empty;
                }
                else
                {
                    _order.Status = OrderStatus.Open;
                    _table.Status = TableStatus.Active;
                    // LastItemAddedAt güncellenmez — sadece yeni ürün eklenince güncellenmeli
                }

                _db.Orders.Update(_order);
                _db.Tables.Update(_table);
                await _db.SaveChangesAsync();

                // SaveChangesAsync başarılı olduktan sonra set et
                // Hata olursa WasFullyPaid yanlış kalmasın
                WasFullyPaid = allItemsPaid;
                PaymentCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ödeme hatası: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
