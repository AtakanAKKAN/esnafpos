using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsnafPos.Models
{
    public class Refund
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; } = null!;
        public decimal Amount { get; set; }
        public string Reason { get; set; } = "";
        public string RefundedBy { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string DayDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}