# TASKS.md — Bekleyen İşler ve Bilinen Sorunlar

---

## Bilinen Buglar / Düzeltilecekler

### 1. İstemci Modunda Lisans Uyarısı Gösteriliyor
**Dosya:** `Views/TablesPage.xaml.cs` → `ShowLicenseBanner()`  
**Dosya:** `Views/AdminPage.xaml.cs` → `LoadLicenseInfo()`  
**Düzeltme:** `if (App.Client != null) return;` ekle  
**Durum:** ✅ Düzeltildi (2026-06-15) — her iki metodun başına client guard eklendi

### 2. DatabaseInitializer'da AppChannels Garantisi
**Dosya:** `Data/DatabaseInitializer.cs`  
**Sorun:** `EnsureCreated()` sonrası `AppChannels` tablosu oluşturulmuyor  
**Düzeltme:** `EnsureCreated()` sonrasına `context.EnsureChannelsTable()` ekle  
**Durum:** ✅ Düzeltildi (2026-06-15) — `Initialize()` içine `EnsureChannelsTable()` eklendi; App.xaml.cs sıralamasından bağımsız çalışır

### 3. Task.Run CS4014 Uyarıları
**Dosya:** `App.xaml.cs`  
**Düzeltme:** Tüm `Task.Run(...)` → `_ = Task.Run(...)` yapıldı  
**Durum:** ✅ Düzeltildi (2026-06-15) — App.xaml.cs'te 4 çağrı da `_ =` ile; doğrulandı

---

## Yakında Eklenecek Özellikler

### Lifetime (Tek Seferlik) Lisans
- Admin panelde lisans üretirken "Süreli" / "Lifetime" seçeneği
- Lifetime = `ExpiryDate: 2099-12-31`, `PackageType: "lifetime"`
- İstemcide "X gün kaldı" uyarısı gösterilmez
- Admin panelde "Ömür Boyu Lisans" yazar
- **Fiyatlandırma:** Süreli ₺599/yıl, Lifetime ₺1.999

### NetworkDiscovery İyileştirme
- Şu an sadece açılışta keşif yapılıyor
- İstemci çalışırken sunucu IP değişirse bağlantı kopuyor
- Periyodik yeniden keşif eklenebilir

---

## Tamamlanan Özellikler

- [x] Masa yönetimi (ekle, sil, sırala, durum takibi)
- [x] Kategori ve ürün yönetimi (3 fiyat: Tam/Az/1.5)
- [x] Sipariş alma (porsiyon seçimi, kanal filtreleme)
- [x] Kısmi ödeme sistemi (ürün bazlı seçim)
- [x] Veresiye sistemi (kayıt + tahsil)
- [x] Masa taşı / birleştir
- [x] ESC/POS yazıcı (USB + LAN)
- [x] Günlük/Haftalık/Aylık raporlar
- [x] Excel export
- [x] Geçmiş hesaplar (sayfalı)
- [x] Ürün değişim/iptal logu
- [x] Kanal sistemi (Masa, Kurye, Trendyol vb.)
- [x] Çok kullanıcı (Admin/Cashier PIN)
- [x] Sunucu-İstemci modu (HTTP API)
- [x] UDP otomatik sunucu keşfi
- [x] Online lisans sistemi (aktivasyon + periyodik kontrol)
- [x] Otomatik güncelleme
- [x] Günlük yedekleme (30 gün saklama)
- [x] Tarih manipülasyonu koruması
- [x] Adisyon yazır (yarım kesim)
- [x] Sürükle-bırak ürün sıralama

---

## Dosya Konumları (Müşteri Bilgisayarında)

```
%AppData%\EsnafPos\
├── esnafpos.db          # Ana veritabanı
├── license.json         # Lisans önbelleği
├── license.backup.json  # Lisans yedek
├── machine.token        # Makine kimliği
├── settings.json        # Uygulama ayarları
├── startup.log          # Başlangıç logu (3 günde bir temizlenir)
├── server_error.log     # API sunucu hataları
└── Yedekler/
    └── esnafpos_2026-06-01.db  # Günlük yedek
```
