using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsnafPos.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int TableId { get; set; }
        public Table Table { get; set; } = null!;
        public string TableNameSnapshot { get; set; } = "";
        public OrderStatus Status { get; set; } = OrderStatus.Open;
        public decimal TotalAmount { get; set; }
        public PaymentType? PaymentType { get; set; }
        public string? PosTransactionId { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }
        public string DayDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
        public DateTime? LastItemAddedAt { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }
}