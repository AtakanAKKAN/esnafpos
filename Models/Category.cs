namespace EsnafPos.Models
{
    public class Category
    {
        public int    Id           { get; set; }
        public string Name         { get; set; } = "";
        public string Channel      { get; set; } = "Masa";  // Masa, Kurye, Bekci, Trendyol, vb.
        public bool   IsActive     { get; set; } = true;
        public int    DisplayOrder { get; set; }
        public List<Product> Products { get; set; } = new();
    }
}
