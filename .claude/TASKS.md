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

### 4. Lisans: geçici I/O hatası masum kullanıcıyı kilitliyordu
**Dosya:** `Services/LicenseService.cs`  
**Sorun 1:** `TryReadCacheFile` geçici dosya kilidini (antivirüs/yedekleme/eşzamanlı yazma) `null` döndürerek "dosya yok/bozuk" sanıyordu → periyodik kontrolde `null` = tamper → "Sistem tarihi hatalı" + Shutdown.  
**Sorun 2:** 24s periyodik kontrol döngü gövdesinde try/catch yoktu → tek `IOException` tüm kontrolü sessizce öldürüyordu.  
**Düzeltme:** (1) Okumada `IOException` için 3 kez retry (150ms); bozuk JSON yine null. (2) Döngü gövdesi try/catch ile sarıldı — geçici hatada tur atlanır.  
**ÖNEMLİ:** Tarih-manipülasyonu **mantığına dokunulmadı** (müşteri kararı). Sadece I/O dayanıklılığı.  
**Durum:** ✅ Düzeltildi (2026-06-17) — Release derleme temiz, 11 test geçti

---

## Yakında Eklenecek Özellikler

### Lifetime (Tek Seferlik) Lisans
- Admin panelde lisans üretirken "Süreli" / "Lifetime" seçeneği
- Lifetime = `ExpiryDate: 2099-12-31`, `PackageType: "lifetime"`
- İstemcide "X gün kaldı" uyarısı gösterilmez
- Admin panelde "Ömür Boyu Lisans" yazar
- **Fiyatlandırma:** Süreli ₺599/yıl, Lifetime ₺1.999

### Fiş Türkçe glif doğrulaması (YAZICI BAŞINDA)
- Fişe basılan Türkçe karakterler (ESC t 0x12 + cp1254) gerçek termal yazıcıda test edilmeli
- Bozuk çıkarsa `Services/PrinterService.cs` → `SET_CODEPAGE_TURKISH` değeri yazıcıya göre ayarlanmalı

### Dokümantasyon temizliği
- ✅ Tamamlandı (2026-06-17) — ARCHITECTURE.md ve CLAUDE.md'deki eski UDP keşif (port 5151) / IP-bağlı bağlanma bölümleri kaldırıldı; hostname + IPv4/IPv6 dual-stack + 5150 firewall otomasyonuna göre güncellendi.

### NOT — Müşteri kararıyla OLDUĞU GİBİ kalacak
- Para güvenliği onayları (ödeme/ürün silme) ve porsiyon akışı DEĞİŞTİRİLMEYECEK

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
- [x] ~~UDP otomatik sunucu keşfi~~ → KALDIRILDI (yerine hostname + dual-stack)
- [x] Hostname/IP ile bağlanma + IPv4/IPv6 dual-stack + 5150 firewall otomasyonu (2026-06)
- [x] Sunucu/istemci dayanıklılığı: global hata yönetimi + yeniden-çözümleme (2026-06)
- [x] Otomatik testler — EsnafPos.Tests, 11 test (para mantığı) (2026-06)
- [x] Fiş: İşletme ayarlarından + Türkçe + Gün Sonu (Z) özeti (2026-06)
- [x] Kapsamlı Türkçe karakter + okunabilirlik + tipografi cilası (2026-06)
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
