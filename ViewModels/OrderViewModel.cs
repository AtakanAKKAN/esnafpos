using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using EsnafPos.Network;
using EsnafPos.Views;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace EsnafPos.ViewModels
{
    public partial class OrderViewModel : BaseViewModel
    {
        private readonly AppDbContext  _db;
        private readonly PrinterService _printer;
        private readonly SessionService _session;

        [ObservableProperty] private EsnafPos.Models.Table? _currentTable;
        [ObservableProperty] private Order? _currentOrder;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private Category? _selectedCategory;

        [ObservableProperty] private string _selectedChannel = "Masa";
        public ObservableCollection<string> Channels { get; } = new();

        // Kasiyer geri donup tekrar girince true olur → eksiltmede sebep sorulur
        public bool IsReturningVisit { get; private set; } = false;

        // Sebep diyalogunu UI tarafindan cagirmak icin event
        public event Func<string, int, string?>? ReasonRequested;

        public ObservableCollection<Category>  Categories { get; } = new();
        public ObservableCollection<Product>   Products   { get; } = new();
        public ObservableCollection<OrderItem> OrderItems { get; } = new();

        public event Action? PaymentRequested;

        public OrderViewModel(AppDbContext db, PrinterService printer, SessionService session)
        {
            _db      = db;
            _printer = printer;
            _session = session;
        }

        public async Task LoadForTable(EsnafPos.Models.Table table, bool isReturn = false)
        {
            CurrentTable     = table;
            IsReturningVisit = isReturn;

            if (App.Client != null)
            {
                await LoadForTableFromApi(table, isReturn);
                return;
            }

            CurrentOrder = await _db.Orders
                .FirstOrDefaultAsync(o => o.TableId == table.Id
                                       && o.Status == OrderStatus.Open);

            if (CurrentOrder != null)
            {
                var items = await _db.OrderItems
                    .Where(i => i.OrderId == CurrentOrder.Id)
                    .ToListAsync();

                OrderItems.Clear();
                foreach (var item in items)
                {
                    var remaining = item.Quantity - item.CollectedQuantity - item.VeresiyeQuantity;
                    if (remaining <= 0) continue; // tamamen odendi, gosterme
                    // UI icin gecici kopya - DB nesnesini degistirme
                    var display = new OrderItem
                    {
                        Id            = item.Id,
                        OrderId       = item.OrderId,
                        ProductId     = item.ProductId,
                        NameSnapshot  = item.NameSnapshot,
                        PriceSnapshot = item.PriceSnapshot,
                        Portion       = item.Portion,
                        Quantity      = remaining,
                        CollectedQuantity = 0
                    };
                    OrderItems.Add(display);
                }
                await RecalcTotal();
            }
            else
            {
                OrderItems.Clear();
                TotalAmount = 0;
            }

            var allCats = await _db.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            // Kanalları DB'den al — kullanıcı tanımlı sıra korunur
            var dbChannels = await _db.AppChannels
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => c.Name)
                .ToListAsync();

            // DB'deki kanallardan sadece kategori olan kanalları göster
            var usedChannels = allCats
                .Select(c => string.IsNullOrWhiteSpace(c.Channel) ? "Masa" : c.Channel)
                .Distinct()
                .ToHashSet();

            var channelList = dbChannels.Where(c => usedChannels.Contains(c)).ToList();
            // DB'de olmayan eski kanallar varsa sona ekle
            foreach (var ch in usedChannels.Where(c => !dbChannels.Contains(c)))
                channelList.Add(ch);

            Channels.Clear();
            foreach (var ch in channelList) Channels.Add(ch);

            // Ilk kanali sec
            SelectedChannel = Channels.FirstOrDefault() ?? "Masa";
            await LoadCategoriesForChannel(SelectedChannel, allCats);
        }

        public async Task SaveProductOrderAsync(IList<EsnafPos.Models.Product> orderedProducts)
        {
            for (int i = 0; i < orderedProducts.Count; i++)
            {
                orderedProducts[i].DisplayOrder = i + 1;
                _db.Products.Update(orderedProducts[i]);
            }
            await _db.SaveChangesAsync();
        }

        public async Task SelectChannel(string channel)
        {
            SelectedChannel = channel;

            if (App.Client != null)
            {
                var cats = await App.Client.GetCategoriesAsync();
                Categories.Clear();
                foreach (var cat in cats.Where(ct =>
                    (string.IsNullOrWhiteSpace(ct.Channel) ? "Masa" : ct.Channel) == channel))
                    Categories.Add(cat);

                if (Categories.Any())
                {
                    SelectedCategory = Categories.First();
                    var prods = await App.Client.GetProductsAsync(SelectedCategory.Id);
                    Products.Clear();
                    foreach (var p in prods) Products.Add(p);
                }
                return;
            }

            var allCats = await _db.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
            await LoadCategoriesForChannel(channel, allCats);
        }

        private async Task LoadCategoriesForChannel(string channel, List<Category> allCats)
        {
            var filtered = allCats
                .Where(c => (string.IsNullOrWhiteSpace(c.Channel) ? "Masa" : c.Channel) == channel)
                .ToList();

            Categories.Clear();
            foreach (var c in filtered) Categories.Add(c);

            if (Categories.Any())
                await SelectCategory(Categories.First());
            else
            {
                Products.Clear();
            }
        }

        [RelayCommand]
        private async Task SelectCategory(Category? category)
        {
            if (category == null) return;
            SelectedCategory = category;

            if (App.Client != null)
            {
                var prods = await App.Client.GetProductsAsync(category.Id);
                Products.Clear();
                foreach (var p in prods) Products.Add(p);
                return;
            }

            var products = await _db.Products
                .Where(p => p.CategoryId == category.Id && p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            Products.Clear();
            foreach (var p in products) Products.Add(p);
        }

        private async Task RefreshOrderItemsFromApi(int orderId)
        {
            var client = App.Client!;

            // Sipariş bilgisini de güncelle (CurrentOrder null ise set et)
            if (CurrentOrder == null)
            {
                var orderDto = await client.GetOrderForTableAsync(CurrentTable!.Id);
                if (orderDto != null)
                {
                    CurrentOrder = new Order
                    {
                        Id                = orderDto.Id,
                        TableId           = orderDto.TableId,
                        TableNameSnapshot = orderDto.TableNameSnapshot,
                        Status            = OrderStatus.Open,
                        TotalAmount       = orderDto.TotalAmount,
                        CreatedAt         = orderDto.CreatedAt,
                        LastItemAddedAt   = orderDto.LastItemAddedAt
                    };
                }
            }

            var items = await client.GetOrderItemsAsync(orderId);
            OrderItems.Clear();
            foreach (var dto in items)
            {
                var remaining = dto.Quantity - dto.CollectedQuantity - dto.VeresiyeQuantity;
                if (remaining <= 0) continue;
                OrderItems.Add(new OrderItem
                {
                    Id            = dto.Id,
                    OrderId       = dto.OrderId,
                    ProductId     = dto.ProductId,
                    NameSnapshot  = dto.NameSnapshot,
                    PriceSnapshot = dto.PriceSnapshot,
                    Portion       = dto.Portion,
                    Quantity      = remaining
                });
            }
            TotalAmount = OrderItems.Sum(i => i.LineTotal);

            if (!OrderItems.Any())
                CurrentOrder = null;
        }

        private async Task LoadForTableFromApi(EsnafPos.Models.Table table, bool isReturn)
        {
            var client = App.Client!;
            var orderDto = await client.GetOrderForTableAsync(table.Id);
            OrderItems.Clear();
            TotalAmount = 0;

            if (orderDto != null)
            {
                CurrentOrder = new Order
                {
                    Id                = orderDto.Id,
                    TableId           = orderDto.TableId,
                    TableNameSnapshot = orderDto.TableNameSnapshot,
                    Status            = OrderStatus.Open,
                    TotalAmount       = orderDto.TotalAmount,
                    CreatedAt         = orderDto.CreatedAt,
                    LastItemAddedAt   = orderDto.LastItemAddedAt
                };

                var items = await client.GetOrderItemsAsync(orderDto.Id);
                foreach (var dto in items)
                {
                    var remaining = dto.Quantity - dto.CollectedQuantity - dto.VeresiyeQuantity;
                    if (remaining <= 0) continue;
                    OrderItems.Add(new OrderItem
                    {
                        Id            = dto.Id,
                        OrderId       = dto.OrderId,
                        ProductId     = dto.ProductId,
                        NameSnapshot  = dto.NameSnapshot,
                        PriceSnapshot = dto.PriceSnapshot,
                        Portion       = dto.Portion,
                        Quantity      = remaining
                    });
                }
                TotalAmount = OrderItems.Sum(i => i.LineTotal);
            }
            else
            {
                CurrentOrder = null;
            }

            // Kategorileri API'den yükle
            var cats = await client.GetCategoriesAsync();
            // API modunda sıralama — kategori sırasını koru
            var channelList = cats
                .Select(c => string.IsNullOrWhiteSpace(c.Channel) ? "Masa" : c.Channel)
                .Distinct()
                .ToList();

            Channels.Clear();
            foreach (var ch in channelList) Channels.Add(ch);

            Categories.Clear();
            var firstChannel = Channels.FirstOrDefault() ?? "Masa";
            SelectedChannel = firstChannel;
            foreach (var cat in cats.Where(ct =>
                (string.IsNullOrWhiteSpace(ct.Channel) ? "Masa" : ct.Channel) == firstChannel))
                Categories.Add(cat);

            if (Categories.Any())
            {
                SelectedCategory = Categories.First();
                var prods = await client.GetProductsAsync(SelectedCategory.Id);
                Products.Clear();
                foreach (var p in prods) Products.Add(p);
            }
        }

        public async Task AddProductWithMode(Product? product, EsnafPos.Views.PortionMode mode)
        {
            if (product == null || CurrentTable == null) return;

            string portion;
            decimal price;

            // Mod belirleme: Az veya 1.5 fiyatı yoksa Tam'a düş
            switch (mode)
            {
                case EsnafPos.Views.PortionMode.Az when product.HasPriceAz:
                    portion = "Az";
                    price   = product.PriceAz!.Value;
                    break;
                case EsnafPos.Views.PortionMode.Bucuk when product.HasPriceBucuk:
                    portion = "1.5 Porsiyon";
                    price   = product.PriceBucuk!.Value;
                    break;
                default:
                    portion = "Tam";
                    price   = product.PriceTam;
                    break;
            }

            if (App.Client != null)
            {
                // Seçili kanal ve kategoriyi kaydet — API çağrısı sonrası geri yükle
                var savedChannel    = SelectedChannel;
                var savedCategoryId = SelectedCategory?.Id;

                var req = new AddProductRequest
                {
                    ProductId     = product.Id,
                    NameSnapshot  = product.Name,
                    PriceSnapshot = price,
                    Portion       = portion
                };
                var orderId = await App.Client.AddProductAsync(CurrentTable!.Id, req);
                if (orderId.HasValue)
                    await RefreshOrderItemsFromApi(orderId.Value);
                else
                    ErrorMessage = "Ürün eklenemedi. Bağlantı kontrol edin.";

                // Kanal ve kategoriyi geri yükle
                if (SelectedChannel != savedChannel)
                    SelectedChannel = savedChannel;
                if (savedCategoryId.HasValue && SelectedCategory?.Id != savedCategoryId)
                {
                    var cat = Categories.FirstOrDefault(c => c.Id == savedCategoryId);
                    if (cat != null)
                    {
                        SelectedCategory = cat;
                        // Ürün listesini de geri yükle
                        var prods = await App.Client.GetProductsAsync(cat.Id);
                        Products.Clear();
                        foreach (var p in prods) Products.Add(p);
                    }
                }
                return;
            }

            await AddProductInternal(product, portion, price);
        }

        [RelayCommand]
        private async Task AddProduct(Product? product)
        {
            // Artık kullanılmıyor - AddProductWithMode kullanılıyor
            // Eski PortionWindow mantığı kaldırıldı
            await AddProductWithMode(product, EsnafPos.Views.PortionMode.Tam);
        }

        private async Task AddProductInternal(Product? product, string portion, decimal price)
        {
            if (product == null || CurrentTable == null) return;

            if (CurrentOrder == null)
            {
                CurrentOrder = new Order
                {
                    TableId           = CurrentTable.Id,
                    TableNameSnapshot = CurrentTable.Name,
                    Status            = OrderStatus.Open,
                    DayDate           = DateTime.Today.ToString("yyyy-MM-dd"),
                    CreatedAt         = DateTime.Now
                };
                _db.Orders.Add(CurrentOrder);
                CurrentTable.Status = TableStatus.Active;
                _db.Tables.Update(CurrentTable);
                await _db.SaveChangesAsync();
            }

            var existing = OrderItems.FirstOrDefault(i =>
                i.ProductId == product.Id && i.Portion == portion);

            if (existing != null)
            {
                // FindAsync ile DB entity guncelle
                var dbExisting = await _db.OrderItems.FindAsync(existing.Id);
                if (dbExisting != null) { dbExisting.Quantity++; }
                if (!ReferenceEquals(existing, dbExisting)) existing.Quantity++;
                else existing.Quantity = dbExisting.Quantity;
                if (CurrentOrder != null) { CurrentOrder.LastItemAddedAt = DateTime.Now; _db.Orders.Update(CurrentOrder); }
                await _db.SaveChangesAsync();
            }
            else
            {
                var newItem = new OrderItem
                {
                    OrderId       = CurrentOrder.Id,
                    ProductId     = product.Id,
                    NameSnapshot  = product.Name,
                    PriceSnapshot = price,
                    Portion       = portion,
                    Quantity      = 1
                };
                _db.OrderItems.Add(newItem);
                if (CurrentOrder != null) { CurrentOrder.LastItemAddedAt = DateTime.Now; _db.Orders.Update(CurrentOrder); }
                await _db.SaveChangesAsync(); // ID atansin
                // DB entity degil, display kopya ekle (AddQuantity'de double-increment olmamasi icin)
                var displayNew = new OrderItem
                {
                    Id            = newItem.Id,
                    OrderId       = newItem.OrderId,
                    ProductId     = newItem.ProductId,
                    NameSnapshot  = newItem.NameSnapshot,
                    PriceSnapshot = newItem.PriceSnapshot,
                    Portion       = newItem.Portion,
                    Quantity      = 1
                };
                OrderItems.Add(displayNew);
            }

            await RecalcTotal();
        }

        [RelayCommand]
        private async Task RemoveItem(OrderItem? item)
        {
            if (item == null) return;

            // Geri donus ziyareti ise sebep sor
            if (IsReturningVisit)
            {
                var reason = ReasonRequested?.Invoke(item.DisplayName, 1);
                if (reason == null) return;  // Dialog iptal edildi

                // Logu kaydet
                await SaveChangeLog(item, 1, reason);
            }

            if (App.Client != null)
            {
                await App.Client.RemoveItemAsync(item.Id);
                if (CurrentOrder != null)
                    await RefreshOrderItemsFromApi(CurrentOrder.Id);
                else
                    await LoadForTable(CurrentTable!, IsReturningVisit);
                return;
            }

            var dbItem = await _db.OrderItems.FindAsync(item.Id);
            if (dbItem != null)
            {
                if (dbItem.Quantity > 1)
                    dbItem.Quantity--;
                else
                    _db.OrderItems.Remove(dbItem);
                await _db.SaveChangesAsync();
            }

            if (item.Quantity > 1)
                item.Quantity--;
            else
                OrderItems.Remove(item);

            await RecalcTotal();

            if (!OrderItems.Any() && CurrentOrder != null && CurrentTable != null)
            {
                _db.Orders.Remove(CurrentOrder);
                CurrentTable.Status = TableStatus.Empty;
                _db.Tables.Update(CurrentTable);
                await _db.SaveChangesAsync();
                CurrentOrder = null;
            }
        }

        [RelayCommand]
        private async Task AddQuantity(OrderItem? item)
        {
            if (item == null) return;
            if (App.Client != null)
            {
                var savedCategoryId2 = SelectedCategory?.Id;
                await App.Client.AddQuantityAsync(item.Id);
                if (CurrentOrder != null)
                    await RefreshOrderItemsFromApi(CurrentOrder.Id);
                if (savedCategoryId2.HasValue && SelectedCategory?.Id != savedCategoryId2)
                {
                    var cat = Categories.FirstOrDefault(c => c.Id == savedCategoryId2);
                    if (cat != null) SelectedCategory = cat;
                }
                return;
            }
            var dbItem = await _db.OrderItems.FindAsync(item.Id);
            if (dbItem == null) return;
            dbItem.Quantity++;
            await _db.SaveChangesAsync();
            if (!ReferenceEquals(item, dbItem)) item.Quantity++;
            await RecalcTotal();
        }

        private async Task SaveChangeLog(OrderItem item, int qty, string reason)
        {
            var log = new OrderChangeLog
            {
                OrderId         = item.OrderId,
                TableName       = CurrentTable?.Name ?? "",
                ProductName     = item.NameSnapshot,
                Portion         = item.Portion,
                QuantityRemoved = qty,
                UnitPrice       = item.PriceSnapshot,
                Reason          = reason,
                CashierName     = _session.CurrentUsername ?? "",
                DayDate         = DateTime.Today.ToString("yyyy-MM-dd"),
                CreatedAt       = DateTime.Now
            };
            _db.OrderChangeLogs.Add(log);
            await _db.SaveChangesAsync();
        }

        // ─── MASA TAŞI ────────────────────────────────────────

        public async Task<string?> MoveToTable(EsnafPos.Models.Table target)
        {
            if (CurrentOrder == null || CurrentTable == null)
                return "Aktif sipariş yok.";

            // Hedef masada açık sipariş var mı?
            var existing = await _db.Orders.FirstOrDefaultAsync(o =>
                o.TableId == target.Id && o.Status == OrderStatus.Open);
            if (existing != null)
                return $"{target.Name} masasında zaten açık bir sipariş var!";

            // Siparisi taşı
            CurrentOrder.TableId           = target.Id;
            CurrentOrder.TableNameSnapshot = target.Name;
            _db.Orders.Update(CurrentOrder);

            // Eski masayı boşalt
            CurrentTable.Status = TableStatus.Empty;
            _db.Tables.Update(CurrentTable);

            // Yeni masayı aktif yap
            target.Status = TableStatus.Active;
            _db.Tables.Update(target);

            await _db.SaveChangesAsync();

            CurrentTable = target;
            return null; // başarılı
        }

        // ─── VERESİYEYE TAŞI ─────────────────────────────────────

        public async Task<string?> MoveToVeresiye(string customerName)
        {
            if (CurrentOrder == null || CurrentTable == null)
                return "Aktif sipariş yok.";

            var items = await _db.OrderItems
                .Where(i => i.OrderId == CurrentOrder.Id)
                .ToListAsync();

            var remaining = items.Where(i =>
                i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity > 0).ToList();

            if (!remaining.Any())
                return "Masada ödenecek ürün yok.";

            foreach (var item in remaining)
            {
                int qty = item.Quantity - item.CollectedQuantity - item.VeresiyeQuantity;
                decimal amount = qty * item.PriceSnapshot;

                // Veresiye payment kaydı
                _db.Payments.Add(new Payment
                {
                    OrderId      = CurrentOrder.Id,
                    Amount       = amount,
                    PaymentType  = PaymentType.Veresiye,
                    CustomerName = customerName.Trim(),
                    CreatedAt    = DateTime.Now
                });

                item.VeresiyeQuantity += qty;
                _db.OrderItems.Update(item);
            }

            // Siparişi kapat
            CurrentOrder.Status    = OrderStatus.Veresiye;
            CurrentOrder.ClosedAt  = DateTime.Now;
            CurrentOrder.TotalAmount = items.Sum(i => i.Quantity * i.PriceSnapshot);
            _db.Orders.Update(CurrentOrder);

            // Masayı boşalt
            CurrentTable.Status = TableStatus.Empty;
            _db.Tables.Update(CurrentTable);

            await _db.SaveChangesAsync();
            return null; // başarılı
        }

        // ─── MASA BİRLEŞTİR ──────────────────────────────────────

        public async Task<string?> MergeFromTable(EsnafPos.Models.Table source)
        {
            if (CurrentOrder == null || CurrentTable == null)
                return "Aktif sipariş yok.";

            // Kaynak masanın siparişini al
            var sourceOrder = await _db.Orders
                .FirstOrDefaultAsync(o => o.TableId == source.Id && o.Status == OrderStatus.Open);
            if (sourceOrder == null)
                return $"{source.Name} masasında açık sipariş bulunamadı.";

            var sourceItems = await _db.OrderItems
                .Where(i => i.OrderId == sourceOrder.Id)
                .ToListAsync();

            var targetItems = await _db.OrderItems
                .Where(i => i.OrderId == CurrentOrder.Id)
                .ToListAsync();

            foreach (var srcItem in sourceItems)
            {
                // Aynı ürün + porsiyon varsa quantity topla
                var match = targetItems.FirstOrDefault(t =>
                    t.ProductId == srcItem.ProductId && t.Portion == srcItem.Portion);

                if (match != null)
                {
                    match.Quantity += srcItem.Quantity - srcItem.CollectedQuantity - srcItem.VeresiyeQuantity;
                    _db.OrderItems.Update(match);
                }
                else
                {
                    var newItem = new OrderItem
                    {
                        OrderId       = CurrentOrder.Id,
                        ProductId     = srcItem.ProductId,
                        NameSnapshot  = srcItem.NameSnapshot,
                        PriceSnapshot = srcItem.PriceSnapshot,
                        Portion       = srcItem.Portion,
                        Quantity      = srcItem.Quantity - srcItem.CollectedQuantity - srcItem.VeresiyeQuantity
                    };
                    _db.OrderItems.Add(newItem);
                }

                // Kaynak item'ı collected olarak işaretle (silme!)
                srcItem.CollectedQuantity = srcItem.Quantity;
                _db.OrderItems.Update(srcItem);
            }

            // Kaynak siparişi iptal et (birleştirildi)
            // Paid değil Cancelled — Payment kaydı yok, Geçmiş Hesaplar'da karışmasın
            sourceOrder.Status      = OrderStatus.Cancelled;
            sourceOrder.ClosedAt    = DateTime.Now;
            sourceOrder.TotalAmount = 0; // ürünler hedef masaya taşındı
            _db.Orders.Update(sourceOrder);

            // Kaynak masayı boşalt
            source.Status = TableStatus.Empty;
            _db.Tables.Update(source);

            await _db.SaveChangesAsync();

            // UI'ı yenile
            await LoadForTable(CurrentTable, IsReturningVisit);
            return null;
        }

        [RelayCommand]
        private void RequestPayment()
        {
            if (CurrentOrder == null || !OrderItems.Any()) return;
            PaymentRequested?.Invoke();
        }

        [RelayCommand]
        private async Task PrintCheck()
        {
            if (CurrentOrder == null || !OrderItems.Any())
            {
                ErrorMessage = "Siparişte ürün yok.";
                return;
            }
            await _printer.PrintCheck(CurrentOrder, OrderItems.ToList());
        }

        private async Task RecalcTotal()
        {
            TotalAmount = OrderItems.Sum(i => i.LineTotal);
            if (CurrentOrder != null)
            {
                CurrentOrder.TotalAmount = TotalAmount;
                _db.Orders.Update(CurrentOrder);
                await _db.SaveChangesAsync();
            }
        }
    }
}
