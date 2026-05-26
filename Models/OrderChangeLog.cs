namespace EsnafPos.Models
{
    public class OrderChangeLog
    {
        public int     Id              { get; set; }
        public int     OrderId         { get; set; }
        public string  TableName       { get; set; } = "";
        public string  ProductName     { get; set; } = "";
        public string  Portion         { get; set; } = "";
        public int     QuantityRemoved { get; set; }
        public decimal UnitPrice       { get; set; }
        public string  Reason          { get; set; } = "";  // "Urun Degisimi" | "Urun Iptali"
        public string  CashierName     { get; set; } = "";
        public string  DayDate         { get; set; } = "";
        public DateTime CreatedAt      { get; set; } = DateTime.Now;
    }
}
