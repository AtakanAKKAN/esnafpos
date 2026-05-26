namespace EsnafPos.Models
{
    public enum TableStatus
    {
        Empty,
        Active,
        PaymentPending
    }

    public enum PaymentType
    {
        Cash,
        CardDebit,
        CardCredit,
        Veresiye
    }

    public enum OrderStatus
    {
        Open,
        Paid,
        Cancelled,
        Refunded,
        Veresiye    // Odeme yapildi fakat veresiye kismı henuz tahsil edilmedi
    }

    public enum UserRole
    {
        Admin,
        Cashier
    }

    public enum PortionType
    {
        Az,
        Tam,
        BuçukFazla
    }
}
