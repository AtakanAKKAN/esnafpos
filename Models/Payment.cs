namespace EsnafPos.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public decimal Amount { get; set; }
        public PaymentType PaymentType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? CustomerName { get; set; }

        public string PaymentTypeDisplay => PaymentType switch
        {
            PaymentType.Cash       => "Nakit",
            PaymentType.CardDebit  => "Banka Karti",
            PaymentType.CardCredit => "Kredi Karti",
            PaymentType.Veresiye   => "Veresiye",
            _ => PaymentType.ToString()
        };
    }
}