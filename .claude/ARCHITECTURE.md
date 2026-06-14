# ARCHITECTURE.md — EsnafPos Teknik Mimari

---

## Veri Akışı

### Standalone Mod
```
UI (WPF) → ViewModel → AppDbContext (SQLite) → Disk
```

### Sunucu Modu
```
UI (WPF) → ViewModel → AppDbContext (SQLite) → Disk
                    ↑
              ApiServer (HTTP :5150) ← İstemci bilgisayarları
              Discovery (UDP :5151)  ← İstemci keşfi
```

### İstemci Modu
```
UI (WPF) → ViewModel → ApiClient → HTTP → Sunucu bilgisayar
```

---

## ViewModel Yapısı

Her ViewModel `BaseViewModel`'dan türer:
```csharp
public abstract class BaseViewModel : ObservableObject
{
    public bool IsBusy { get; set; }
    public string ErrorMessage { get; set; }
}
```

### OrderViewModel — En Kritik ViewModel
İki farklı kod yolu var:
```csharp
if (App.Client != null)
{
    // HTTP API üzerinden sunucuya git
    await App.Client.AddProductAsync(...);
}
else
{
    // Direkt DB'ye yaz
    _db.OrderItems.Add(...);
}
```

---

## Ödeme Akışı (PaymentViewModel)

```
1. Kullanıcı ürünleri seçer (PaymentItemRow.SelectedQuantity)
2. Ödeme yöntemi seçer (Nakit/Kart/Veresiye)
3. PaymentEntry listesi oluşur (ConsumedItems ile)
4. CompletePaymentWithEntries() çağrılır
5. Her item için:
   - Nakit/Kart → CollectedQuantity += qty
   - Veresiye   → VeresiyeQuantity += qty
6. Tüm items paid mi? → Order.Status = Paid/Veresiye
7. Masa boşalır → Table.Status = Empty
```

---

## Kanal Sistemi

Kategoriler kanallara ayrılır:
- **Masa** → Normal restoran masası siparişi
- **Kurye** → Paket servis
- **Bekçi** → Kapıda bekleyen
- **Trendyol/Yemeksepeti** → Online platform siparişi
- **Diğer** → Özel durumlar

Kanallar `AppChannels` tablosunda tutulur, admin panelinden yönetilir.

Sipariş ekranında kanal butonları DB'den dinamik gelir:
```csharp
var dbChannels = await _db.AppChannels
    .Where(c => c.IsActive)
    .OrderBy(c => c.DisplayOrder)
    .Select(c => c.Name)
    .ToListAsync();
```

---

## Veresiye Sistemi

Veresiye = müşteri şimdi ödemez, sonra öder:

```
Payment kaydı → PaymentType = Veresiye, CustomerName = "Ahmet"
OrderItem     → VeresiyeQuantity += qty
Order.Status  → Veresiye (tümü veresiye ise) veya Open (kısmi)
```

**Tahsil edilince (VeresiyeDetailWindow):**
```
OrderItem.VeresiyeQuantity -= tahsiledilen
OrderItem.CollectedQuantity += tahsiledilen
Payment.PaymentType → Cash (veya yeni Cash payment eklenir)
Order.Status → Paid (tümü tahsil edildiyse)
```

---

## Masa İşlemleri

### Taşı (MoveToTable)
```
Sipariş.TableId = hedef masa
Kaynak masa → Empty
Hedef masa  → Active
```

### Birleştir (MergeFromTable)
```
Kaynak masanın items → Hedef masanın orderına taşınır
Aynı ürün+porsiyon varsa → Quantity toplanır
Kaynak item.CollectedQuantity = item.Quantity (sıfırlanmış gibi işaretlenir)
Kaynak sipariş → Cancelled
Kaynak masa   → Empty
```

### Veresiyeye Taşı (MoveToVeresiye)
```
Kalan tüm items → Veresiye payment olarak kaydedilir
Sipariş → Veresiye
Masa    → Empty
```

---

## Rapor Hesaplama Mantığı

### Geçerli Siparişler (validOrderIds)
```csharp
// Nakit/Kart ödemesi olan siparişler
var paidOrderIds = Payments
    .Where(p => p.PaymentType == Cash || CardDebit || CardCredit)
    .Select(p => p.OrderId);

// Aktif veresiyesi olan siparişler
var activeVeresiyeOrderIds = Payments
    .Where(p => p.PaymentType == Veresiye)
    .Select(p => p.OrderId);

var validOrderIds = paidOrderIds.Union(activeVeresiyeOrderIds);
```

**Önemli:** Veresiyesi silinmiş siparişler rapora dahil edilmez.

### Ürün Satış Miktarı
```csharp
TotalQuantity = CollectedQuantity + VeresiyeQuantity > 0
    ? CollectedQuantity + VeresiyeQuantity
    : Quantity
```

---

## Güncelleme Sistemi

```
1. App açılır
2. _ = Task.Run(async () => UpdateService.CheckForUpdateAsync())
3. Render.com'a istek: GET /api/update/check?currentVersion=2.0.0
4. Yeni versiyon varsa → UpdateWindow göster
5. Kullanıcı kabul ederse → .exe indir → .bat script yaz → Kapat
6. .bat script: eski .exe'yi yeni ile değiştir → yeniden başlat
```

`IsForced = true` ise kullanıcı "Sonra Hatırlat" yapamaz, uygulama kapanır.

---

## Yazıcı Entegrasyonu

**ESC/POS protokolü** kullanılır (termal yazıcılar).

**Bağlantı tipleri:**
- **USB:** Windows Print API ile (RawPrint)
- **LAN:** TCP socket (port 9100)

**Kod sayfası:** Windows-1254 (Türkçe karakter desteği)

**Fiş tipleri:**
- `PrintReceipt()` → Tam kesim (ödeme sonrası)
- `PrintCheck()` → Yarım kesim (adisyon)

---

## Güvenlik Duvarı Gereksinimleri

Sunucu bilgisayarda açık olması gereken portlar:
```powershell
# HTTP API (zorunlu)
New-NetFirewallRule -DisplayName "EsnafPos API" -Direction Inbound -Protocol TCP -LocalPort 5150 -Action Allow

# UDP Discovery (otomatik IP keşfi için)
New-NetFirewallRule -DisplayName "EsnafPos Discovery" -Direction Inbound -Protocol UDP -LocalPort 5151 -Action Allow
```

---

## Lisans Güvenlik Mekanizmaları

### SHA256 İmza
```csharp
var raw = $"{token}|{ExpiryDate:O}|{PackageType}|{CloseRemainingDays}|{LastCheckedRemaining}|{Secret}";
```

### Tarih Manipülasyonu Tespiti
```
CloseRemainingDays   = Uygulama kapanırken kaydedilen kalan gün
LastCheckedRemaining = Son 24 saatlik kontroldeki kalan gün
Startup'ta: current > threshold → Invalid (tarih geri alınmış)
```

### Yedek Dosya
```
license.json        → Ana dosya
license.backup.json → Her yazımda kopyalanır, ana bozulursa restore edilir
```
