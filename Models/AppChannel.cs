namespace EsnafPos.Models
{
    public class AppChannel
    {
        public int    Id           { get; set; }
        public string Name         { get; set; } = "";
        public int    DisplayOrder { get; set; }
        public bool   IsActive     { get; set; } = true;
    }
}
