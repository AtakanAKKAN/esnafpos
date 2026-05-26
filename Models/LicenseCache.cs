namespace EsnafPos.Models
{
    public class LicenseCache
    {
        public string   MachineToken { get; set; } = "";
        public DateTime ExpiryDate   { get; set; }
        public DateTime ServerDate   { get; set; }
        public string   PackageType  { get; set; } = "single";
        public string   BusinessName { get; set; } = "";
        public string   Signature    { get; set; } = "";

        // ─── Kalan gün takibi (tarih manipülasyonu tespiti) ───────────
        /// <summary>Uygulama kapanırken kaydedilen kalan gün sayısı.</summary>
        public int      CloseRemainingDays   { get; set; } = 9999;

        /// <summary>Son 24 saatlik kontrolde kaydedilen kalan gün sayısı.</summary>
        public int      LastCheckedRemaining { get; set; } = 9999;

        /// <summary>Son 24 saatlik kontrolün zamanı.</summary>
        public DateTime LastCheckedTime      { get; set; } = DateTime.MinValue;
    }
}
