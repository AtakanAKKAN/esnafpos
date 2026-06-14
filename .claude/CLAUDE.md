# CLAUDE.md — EsnafPos Proje Rehberi

Bu dosya Claude Code'un projeyi anlaması için hazırlanmıştır. Yeni bir oturumda **önce bu dosyayı oku**.

---

## Proje Özeti

**EsnafPos**, küçük restoran ve kafeler için geliştirilmiş offline-first bir POS (Point of Sale) uygulamasıdır. İnternet kesilse bile çalışmaya devam eder — bu en kritik özelliğidir.

**Pazarlama markası:** Kesintisizpos  
**Geliştirici:** Atakan  
**Hedef kitle:** Türkiye'deki küçük restoranlar ve kafeler  
**Lisans sunucusu:** https://esnaflisans.onrender.com  
**Landing page:** https://kesintisizpos.com.tr  

---

## Tech Stack

| Katman | Teknoloji |
|--------|-----------|
| UI | WPF (.NET 8, Windows) |
| Veritabanı | SQLite (EF Core 8) |
| API | Gömülü ASP.NET Core Minimal API |
| MVVM | CommunityToolkit.Mvvm 8.3.2 |
| Excel | ClosedXML |
| Yazıcı | ESCPOS_NET (ESC/POS termal yazıcı) |
| DI | Microsoft.Extensions.DependencyInjection |

---

## Çalışma Modları

Uygulama 3 modda çalışır (`AppMode` enum):

### 1. Standalone (Tek Bilgisayar)
- Hem UI hem DB aynı bilgisayarda
- Küçük işletmeler için varsayılan mod

### 2. Server (Sunucu)
- DB ve API bu bilgisayarda
- Port 5150'de HTTP API açar
- Port 5151'de UDP discovery dinler
- `App.Server = new ApiServer(...)`

### 3. Client (İstemci)
- 2. ekran bilgisayarı
- Tüm işlemler HTTP API üzerinden sunucuya gider
- Açılışta UDP broadcast ile sunucuyu otomatik bulur (NetworkDiscovery)
- `App.Client = new ApiClient(...)`
- **Lisans kontrolü YAPILMAZ** — sadece sunucuda yapılır

---

## Klasör Yapısı

```
EsnafPos/
├── App.xaml.cs              # Uygulama başlangıcı, DI kurulumu, mod seçimi
├── Models/                  # Entity sınıfları
│   ├── Order.cs             # Sipariş
│   ├── OrderItem.cs         # Sipariş kalemi (asla fiziksel silinmez)
│   ├── Table.cs             # Masa
│   ├── Product.cs           # Ürün (PriceTam, PriceAz, PriceBucuk)
│   ├── Category.cs          # Kategori (Channel alanı ile kanal bilgisi)
│   ├── Payment.cs           # Ödeme kaydı
│   ├── AppChannel.cs        # Sipariş kanalı (Masa, Kurye, Trendyol vb.)
│   ├── User.cs              # Kullanıcı (Admin/Cashier)
│   ├── OrderChangeLog.cs    # Ürün iptal/değişim logu
│   └── LicenseCache.cs      # Lisans önbelleği
├── Data/
│   ├── AppDbContext.cs      # EF Core context + Ensure metodları
│   └── DatabaseInitializer.cs # İlk kurulum seed'i
├── Network/
│   ├── ApiServer.cs         # Gömülü HTTP API (sunucu modu)
│   ├── ApiClient.cs         # HTTP istemci (istemci modu)
│   ├── NetworkDiscovery.cs  # UDP otomatik sunucu keşfi
│   └── NetworkSettings.cs   # AppMode enum + ayarlar
├── Services/
│   ├── LicenseService.cs    # Lisans aktivasyon/kontrol/önbellek
│   ├── SettingsService.cs   # settings.json okuma/yazma
│   ├── PrinterService.cs    # ESC/POS yazıcı (USB veya LAN)
│   ├── SessionService.cs    # Oturum (giriş yapan kullanıcı)
│   ├── ExcelExportService.cs # Rapor Excel export
│   └── UpdateService.cs     # Otomatik güncelleme
├── ViewModels/
│   ├── TablesViewModel.cs   # Masalar ekranı
│   ├── OrderViewModel.cs    # Sipariş ekranı
│   ├── PaymentViewModel.cs  # Ödeme ekranı
│   ├── AdminViewModel.cs    # Yönetim paneli
│   ├── ReportsViewModel.cs  # Raporlar
│   └── LoginViewModel.cs    # Giriş ekranı
└── Views/
    ├── TablesPage.xaml(.cs)
    ├── OrderPage.xaml(.cs)
    ├── PaymentWindow.xaml(.cs)
    ├── AdminPage.xaml(.cs)
    ├── ReportsPage.xaml(.cs)
    ├── LoginWindow.xaml(.cs)
    ├── OrderHistoryWindow.xaml(.cs)
    ├── ChangeLogWindow.xaml(.cs)
    ├── VeresiyeDetailWindow.xaml(.cs)
    ├── TablePickerWindow.xaml(.cs)
    ├── EditCategoryDialog.xaml(.cs)
    ├── EditProductDialog.xaml(.cs)
    ├── UpdateWindow.xaml(.cs)
    └── ActivationWindow.xaml(.cs)
```

---

## Kritik İş Kuralları

Bu kuralları **asla ihlal etme:**

### 1. OrderItem asla fiziksel silinmez
```csharp
// YANLIŞ — yapma
_db.OrderItems.Remove(item);

// DOĞRU — quantity 0'a indir
if (item.Quantity > 1) item.Quantity--;
else item.Quantity = 0;
```
Sebep: Geçmiş raporlar ve ürün satış istatistikleri bozulur.

### 2. Porsiyon sistemi
Her ürünün 3 fiyatı olabilir:
- `PriceTam` — zorunlu, her üründe var
- `PriceAz` — opsiyonel
- `PriceBucuk` — opsiyonel (1.5 porsiyon)

Sipariş kaleminde `Portion` alanı: `"Tam"`, `"Az"`, `"1.5 Porsiyon"`

### 3. Kalan tutar hesabı
```csharp
// Kalan = ödenmemiş kalemlerin toplamı
var remaining = item.Quantity - item.CollectedQuantity - item.VeresiyeQuantity;
```
- `CollectedQuantity`: Nakit/Kart ile tahsil edilen
- `VeresiyeQuantity`: Veresiyeye yazılan

### 4. Sipariş durumları
```
Open       → Aktif sipariş
Paid       → Tamamen ödendi
Veresiye   → Tamamen veresiyeye yazıldı
Cancelled  → İptal edildi (masa birleştirmede kaynak masa)
```

### 5. İstemci modunda lisans kontrolü yapılmaz
```csharp
// App.xaml.cs'te lisans bloğu:
if (tempSettings.Network.Mode != AppMode.Client)
{
    // lisans kontrol et
}
```

---

## Veritabanı

**Konum:** `%AppData%\EsnafPos\esnafpos.db`  
**Migration yok** — `EnsureCreated()` + manuel `Ensure*` metodları kullanılır

### Ensure Metodları (AppDbContext.cs)
Eski kurulumlar için yeni kolonları/tabloları ekler:
```csharp
db.EnsureCustomerNameColumn();    // Payments.CustomerName
db.EnsureChannelsTable();          // AppChannels tablosu
db.EnsureCategoryChannelColumn();  // Categories.Channel
db.EnsureCollectedQuantityColumn();// OrderItems.CollectedQuantity
db.EnsureOrderChangeLogTable();    // OrderChangeLogs tablosu
db.EnsureLastItemAddedAtColumn();  // Orders.LastItemAddedAt
db.EnsureVeresiyeQuantityColumn(); // OrderItems.VeresiyeQuantity
```

**Önemli:** `DatabaseInitializer.Initialize()` içinde de `EnsureChannelsTable()` çağrılmalı — `EnsureCreated()` yeni tabloları mevcut DB'ye eklemez.

---

## Ağ Mimarisi

### API (Port 5150)
- HTTP Basic Auth → `X-Api-Key` header (Base64 username:password)
- Tüm endpoint'ler `/api/` prefix'i ile başlar
- Enum'lar string olarak serialize edilir

### Discovery (Port 5151 UDP)
```
İstemci → UDP Broadcast: "ESNAFPOS_DISCOVER"
Sunucu  → UDP Response:  "ESNAFPOS_SERVER:192.168.1.x:5150"
```
- Timeout: 4000ms
- Bulunamazsa kayıtlı IP'ye fallback

---

## Lisans Sistemi

- **API:** https://esnaflisans.onrender.com
- **Aktivasyon kodu formatı:** `CIKO-XXXX-XXXX`
- **Önbellek:** `%AppData%\EsnafPos\license.json` (SHA256 imzalı)
- **Yedek:** `license.backup.json`
- **Tarih manipülasyonu koruması:** `CloseRemainingDays` ve `LastCheckedRemaining` takibi

### PackageType
- `"single"` — Tek ekran
- `"multi"` — Çok ekran (sunucu + istemci)

---

## Ayarlar

**Konum:** `%AppData%\EsnafPos\settings.json`

```json
{
  "Network": {
    "Mode": "Standalone|Server|Client",
    "ServerIp": "192.168.1.x",
    "ServerPort": 5150,
    "ApiUsername": "esnafpos",
    "ApiPassword": "esnafpos123"
  },
  "Printer": {
    "ConnectionType": "USB|LAN",
    "UsbPrinterName": "",
    "LanIp": "",
    "LanPort": 9100
  },
  "Business": {
    "AppName": "Esnaf POS",
    "BusinessName": "",
    "Address": "",
    "Phone": "",
    "ReceiptNote": ""
  }
}
```

---

## DI Kayıtları (App.xaml.cs)

```csharp
services.AddDbContext<AppDbContext>();           // Transient
services.AddSingleton<SessionService>();
services.AddSingleton<PrinterService>();
services.AddSingleton<SettingsService>();
services.AddSingleton<ExcelExportService>();
services.AddSingleton<LicenseService>();
services.AddSingleton<UpdateService>();
services.AddTransient<LoginViewModel>();
services.AddTransient<TablesViewModel>();
services.AddTransient<OrderViewModel>();
services.AddTransient<PaymentViewModel>();
services.AddTransient<ReportsViewModel>();
services.AddTransient<AdminViewModel>();
services.AddTransient<AdminPage>();
services.AddTransient<ReportsPage>();
services.AddTransient<MainWindow>();
```

---

## Publish

**Profil:** `Properties/PublishProfiles/FolderProfile.pubxml`
- Runtime: `win-x64`
- Single file: `true`
- Self-contained: `true`
- Output: `bin\Release\net8.0-windows\publish\win-x64\`

**Auto-update:** Render.com'dan yeni `.exe` indirir, `.bat` script ile kendini değiştirir.

---

## Sık Yapılan Hatalar

1. **`Task.Run` uyarısı** → `_ = Task.Run(...)` kullan
2. **`async void`** → Sadece event handler'larda kabul edilebilir
3. **İstemcide DB sorgusu** → `if (App.Client != null) return;` kontrolü yap
4. **Yeni tablo ekleme** → Hem `AppDbContext.Ensure*` metodu hem `DatabaseInitializer` içine ekle
5. **OrderItem silme** → Quantity = 0 yap, Remove çağırma
