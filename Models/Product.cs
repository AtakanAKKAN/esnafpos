namespace EsnafPos.Models
{
    public class Product
    {
        public int      Id          { get; set; }
        public int      CategoryId  { get; set; }
        public Category Category    { get; set; } = null!;
        public string   Name        { get; set; } = "";
        public decimal  PriceTam    { get; set; }
        public decimal? PriceAz     { get; set; }
        public decimal? PriceBucuk  { get; set; }

        // Eski uyumluluk
        public decimal  Price       { get => PriceTam; set => PriceTam = value; }

        public bool IsActive     { get; set; } = true;
        public int  DisplayOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Admin UI icin gorsel yardimcilar
        public bool HasPriceAz     => PriceAz.HasValue    && PriceAz.Value    > 0;
        public bool HasPriceBucuk  => PriceBucuk.HasValue && PriceBucuk.Value > 0;

        // Kac aktif fiyat secenegi var (porsiyon secimi icin)
        public int ActivePriceCount =>
            1
            + (PriceAz.HasValue    && PriceAz.Value    > 0 ? 1 : 0)
            + (PriceBucuk.HasValue && PriceBucuk.Value > 0 ? 1 : 0);
    }
}
