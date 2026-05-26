using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace EsnafPos.Models
{
    public class Table : INotifyPropertyChanged
    {
        public int         Id           { get; set; }
        public string      Name         { get; set; } = "";
        public bool        IsActive     { get; set; } = true;
        public int         DisplayOrder { get; set; }

        private TableStatus _status = TableStatus.Empty;
        public TableStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        [NotMapped]
        private decimal _currentTotal;
        [NotMapped]
        public decimal CurrentTotal
        {
            get => _currentTotal;
            set { _currentTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTotal)); }
        }

        [NotMapped]
        public bool HasTotal => CurrentTotal > 0;

        [NotMapped]
        public DateTime? LastItemAddedAt { get; set; }

        [NotMapped]
        private string _elapsedText = "";

        [NotMapped]
        public string ElapsedText
        {
            get => _elapsedText;
            set
            {
                _elapsedText = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
