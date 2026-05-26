using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Network;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace EsnafPos.ViewModels
{
    public class VeresiyeCardItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string CustomerName { get; set; } = "";
        public string Initial => string.IsNullOrEmpty(CustomerName) ? "?"
            : CustomerName[0].ToString().ToUpper();
        public decimal TotalAmount { get; set; }
        public List<int> PaymentIds { get; set; } = new();
        public List<int> OrderIds   { get; set; } = new();
        public DateTime? LastItemAddedAt { get; set; }

        private string _elapsedText = "";
        public string ElapsedText
        {
            get => _elapsedText;
            set
            {
                _elapsedText = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ElapsedText)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class TablesViewModel : BaseViewModel
    {
        private readonly AppDbContext _db;

        public ObservableCollection<object> AllCards { get; } = new();
        // Masa id -> aktif siparis toplami
        public Dictionary<int, decimal> TableTotals { get; } = new();

        [ObservableProperty] private Table? _selectedTable;

        public event Action<Table>? TableSelected;

        public TablesViewModel(AppDbContext db)
        {
            _db = db;
        }

        [RelayCommand]
        public async Task LoadTables()
        {
            IsBusy = true;
            try
            {
                // İstemci modunda API'den yükle
                if (App.Client != null)
                {
                    await LoadTablesFromApi();
                    return;
                }
                var tables = await _db.Tables
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.DisplayOrder)
                    .ToListAsync();

                // Kalan urunlerin toplamini hesapla
                // (Odenen urunler zaten DB'den silindi, dogrudan items toplamini al)
                TableTotals.Clear();
                var activeOrders = await _db.Orders
                    .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Veresiye)
                    .Include(o => o.Items)
                    .ToListAsync();

                foreach (var order in activeOrders)
                {
                    // Kalan adet = Quantity - CollectedQuantity
                    var total = order.Items.Sum(i =>
                        (i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity) * i.PriceSnapshot);
                    if (TableTotals.ContainsKey(order.TableId))
                        TableTotals[order.TableId] += total;
                    else
                        TableTotals[order.TableId] = total;
                }

                // Toplam ve ElapsedText tablolara ata
                foreach (var t in tables)
                {
                    t.CurrentTotal = TableTotals.TryGetValue(t.Id, out var tot) ? tot : 0;
                    // Sadece Open statüsündeki siparişten süre hesapla.
                    // Veresiye statüsündeki sipariş masa boşalsa bile activeOrders'ta kalır,
                    // ondan süre hesaplarsak masa "boş" görünse de sayaç çalışmaya devam eder.
                    var openOrder = activeOrders.FirstOrDefault(o =>
                        o.TableId == t.Id && o.Status == OrderStatus.Open);
                    if (openOrder != null && openOrder.LastItemAddedAt.HasValue)
                    {
                        t.LastItemAddedAt = openOrder.LastItemAddedAt;
                        t.ElapsedText = CalcElapsed(openOrder.LastItemAddedAt.Value);
                    }
                    else
                    {
                        t.LastItemAddedAt = null;
                        t.ElapsedText = "";
                    }
                }

                AllCards.Clear();
                foreach (var t in tables)
                    AllCards.Add(t);

                await AddVeresiyeCards();
            }
            finally { IsBusy = false; }
        }

        private async Task LoadTablesFromApi()
        {
            try
            {
                var client = App.Client!;
                var tableDtos = await client.GetTablesAsync();
                var veresiyePayments = await client.GetVeresiyeAsync();

                AllCards.Clear();
                TableTotals.Clear();

                foreach (var dto in tableDtos.OrderBy(t => t.DisplayOrder))
                {
                    var status = Enum.TryParse<TableStatus>(dto.Status, out var s) ? s : TableStatus.Empty;
                    var table = new Table
                    {
                        Id           = dto.Id,
                        Name         = dto.Name,
                        Status       = status,
                        DisplayOrder = dto.DisplayOrder,
                        CurrentTotal = dto.CurrentTotal,
                        LastItemAddedAt = dto.LastItemAddedAt,
                        ElapsedText  = (status == TableStatus.Active && dto.LastItemAddedAt.HasValue)
                            ? CalcElapsed(dto.LastItemAddedAt.Value) : ""
                    };
                    AllCards.Add(table);
                    if (dto.CurrentTotal > 0)
                        TableTotals[dto.Id] = dto.CurrentTotal;
                }

                // Veresiye kartları
                var grouped = veresiyePayments
                    .GroupBy(p => p.CustomerName?.Trim().ToLower() ?? "")
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .Select(g =>
                    {
                        var latest = g.OrderByDescending(p => p.CreatedAt).First();
                        return new VeresiyeCardItem
                        {
                            CustomerName    = latest.CustomerName!,
                            TotalAmount     = g.Sum(p => p.Amount),
                            PaymentIds      = g.Select(p => p.Id).ToList(),
                            OrderIds        = g.Select(p => p.OrderId).Distinct().ToList(),
                            LastItemAddedAt = latest.CreatedAt,
                            ElapsedText     = CalcElapsed(latest.CreatedAt)
                        };
                    })
                    .OrderBy(v => v.CustomerName);

                foreach (var card in grouped)
                    AllCards.Add(card);
            }
            finally { IsBusy = false; }
        }

        public async Task RefreshVeresiye()
        {
            var toRemove = AllCards.OfType<VeresiyeCardItem>().ToList();
            foreach (var v in toRemove)
                AllCards.Remove(v);

            await AddVeresiyeCards();
        }

        private async Task AddVeresiyeCards()
        {
            var payments = await _db.Payments
                .Where(p => p.PaymentType == PaymentType.Veresiye
                         && p.CustomerName != null)
                .ToListAsync();

            // Veresiye siparişlerinin LastItemAddedAt bilgisini çek
            var allOrderIds = payments.Select(p => p.OrderId).Distinct().ToList();
            var veresiyeOrders = await _db.Orders
                .Where(o => allOrderIds.Contains(o.Id))
                .ToListAsync();

            var grouped = payments
                .GroupBy(p => p.CustomerName!.Trim().ToLower())
                .Select(g => {
                    var orderIdList = g.Select(p => p.OrderId).Distinct().ToList();
                    // Süre masanın açılma zamanından değil, veresiye ödeme kaydının
                    // oluşturulma zamanından (Payment.CreatedAt) hesaplanır.
                    var latestPayment = g.OrderByDescending(p => p.CreatedAt).First();
                    return new VeresiyeCardItem
                    {
                        CustomerName      = latestPayment.CustomerName!,
                        TotalAmount       = g.Sum(p => p.Amount),
                        PaymentIds        = g.Select(p => p.Id).ToList(),
                        OrderIds          = orderIdList,
                        LastItemAddedAt   = latestPayment.CreatedAt,
                        ElapsedText       = CalcElapsed(latestPayment.CreatedAt)
                    };
                })
                .OrderBy(v => v.CustomerName)
                .ToList();

            foreach (var card in grouped)
                AllCards.Add(card);
        }

        /// <summary>
        /// Sunucu modunda istemci değişikliklerini yansıtmak için kullanılır.
        /// AllCards'ı sıfırlamak yerine mevcut Table nesnelerinin property'lerini günceller.
        /// Böylece WPF binding'i değişiklikleri doğru algılar.
        /// </summary>
        public async Task RefreshTablesFromDb()
        {
            try
            {
            // Her refresh'te yeni DbContext scope aç - stale data garantisi yok
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tables = await db.Tables
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder)
                .ToListAsync();

            var activeOrders = await db.Orders
                .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Veresiye)
                .Include(o => o.Items)
                .ToListAsync();

            foreach (var t in tables)
            {
                // Mevcut kart nesnesini bul
                var existing = AllCards.OfType<Table>().FirstOrDefault(x => x.Id == t.Id);
                if (existing == null) continue;

                // Status güncelle
                existing.Status = t.Status;

                // CurrentTotal güncelle
                var total = activeOrders
                    .Where(o => o.TableId == t.Id)
                    .Sum(o => o.Items.Sum(i =>
                        (i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity) * i.PriceSnapshot));
                existing.CurrentTotal = total;

                // ElapsedText güncelle
                var openOrder = activeOrders.FirstOrDefault(o =>
                    o.TableId == t.Id && o.Status == OrderStatus.Open);
                if (openOrder?.LastItemAddedAt.HasValue == true)
                {
                    existing.LastItemAddedAt = openOrder.LastItemAddedAt;
                    existing.ElapsedText = CalcElapsed(openOrder.LastItemAddedAt.Value);
                }
                else
                {
                    existing.LastItemAddedAt = null;
                    existing.ElapsedText = "";
                }
            }
            }
            catch { /* Arka plan yenileme hatası - sessizce geç */ }
        }

        public async Task SaveTableOrderAsync(List<Table> orderedTables)
        {
            for (int i = 0; i < orderedTables.Count; i++)
            {
                orderedTables[i].DisplayOrder = i + 1;
                _db.Tables.Update(orderedTables[i]);
            }
            await _db.SaveChangesAsync();
        }

        public static string CalcElapsed(DateTime since)
        {
            var elapsed = DateTime.Now - since;
            if (elapsed.TotalMinutes < 1) return "Az once";
            if (elapsed.TotalHours < 1)  return $"{(int)elapsed.TotalMinutes} dk";
            var h = (int)elapsed.TotalHours;
            var m = elapsed.Minutes;
            return m > 0 ? $"{h} sa {m} dk" : $"{h} sa";
        }

        [RelayCommand]
        private void SelectTable(Table table)
        {
            SelectedTable = table;
            TableSelected?.Invoke(table);
        }

        public async Task RefreshTable(int tableId)
        {
            var updated = await _db.Tables.FindAsync(tableId);
            if (updated == null) return;

            var existing = AllCards.OfType<Table>().FirstOrDefault(t => t.Id == tableId);
            if (existing != null)
            {
                var index = AllCards.IndexOf(existing);
                AllCards[index] = updated;
            }
        }
    }
}
