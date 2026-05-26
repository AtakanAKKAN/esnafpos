using EsnafPos.Data;
using EsnafPos.Models;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public partial class ChangeLogWindow : Window
    {
        private readonly AppDbContext _db;
        private List<OrderChangeLog> _allLogs      = new();
        private List<OrderChangeLog> _filteredLogs = new();
        private int _currentPage = 0;
        private const int PageSize = 20;

        public ChangeLogWindow(AppDbContext db)
        {
            InitializeComponent();
            _db = db;
            DpFilter.SelectedDate = DateTime.Today;
            Loaded += async (s, e) => await LoadAll();
        }

        private async Task LoadAll()
        {
            _allLogs = await _db.OrderChangeLogs
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            ApplyFilter(
                _allLogs.Where(l => l.DayDate == DateTime.Today.ToString("yyyy-MM-dd")).ToList()
            );
        }

        private void ApplyFilter(List<OrderChangeLog> source)
        {
            _filteredLogs = source;
            _currentPage  = 0;
            ShowPage();
        }

        private void ShowPage()
        {
            var total      = _filteredLogs.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
            var page       = _filteredLogs
                .Skip(_currentPage * PageSize)
                .Take(PageSize)
                .ToList();

            IcLogs.ItemsSource = page;
            TxtCount.Text      = $"{total} kayit";
            TxtPageInfo.Text   = $"Sayfa {_currentPage + 1} / {totalPages}";

            BtnPrev.IsEnabled = _currentPage > 0;
            BtnNext.IsEnabled = (_currentPage + 1) < totalPages;
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (DpFilter.SelectedDate == null) return;
            var dateStr = DpFilter.SelectedDate.Value.ToString("yyyy-MM-dd");
            ApplyFilter(_allLogs.Where(l => l.DayDate == dateStr).ToList());
        }

        private void BtnAll_Click(object sender, RoutedEventArgs e)
            => ApplyFilter(_allLogs);

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                ShowPage();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            var totalPages = (int)Math.Ceiling(_filteredLogs.Count / (double)PageSize);
            if (_currentPage + 1 < totalPages)
            {
                _currentPage++;
                ShowPage();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
