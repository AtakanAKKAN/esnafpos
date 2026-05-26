using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace EsnafPos.Models
{
    public class OrderItem : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string NameSnapshot { get; set; } = "";
        public decimal PriceSnapshot { get; set; }
        public string Portion { get; set; } = "Tam";

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        // Nakit/Kart ile tahsil edildi
        public int CollectedQuantity { get; set; } = 0;

        // Veresiyeye yazildi (henuz nakit tahsil edilmedi)
        public int VeresiyeQuantity { get; set; } = 0;

        [NotMapped]
        public decimal LineTotal => PriceSnapshot * Quantity;

        [NotMapped]
        public string DisplayName => Portion == "Tam" ? NameSnapshot : $"{NameSnapshot} ({Portion})";

        public Order? Order { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
