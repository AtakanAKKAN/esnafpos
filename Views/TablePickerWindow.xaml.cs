using EsnafPos.Data;
using EsnafPos.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public enum TablePickerMode { Move, Merge }

    // Picker'da gösterilecek öğe tipleri
    public class TablePickerTableItem
    {
        public int     Id           { get; set; }
        public string  Name         { get; set; } = "";
        public decimal CurrentTotal { get; set; }
    }

    public class TablePickerVeresiyeItem
    {
        public string  CustomerName { get; set; } = "";
        public decimal TotalAmount  { get; set; }
        public List<int> PaymentIds { get; set; } = new();
    }

    public partial class TablePickerWindow : Window
    {
        // Seçilen öğe - ya masa ya veresiye müşterisi
        public TablePickerTableItem?    SelectedTableItem    { get; private set; }
        public TablePickerVeresiyeItem? SelectedVeresiyeItem { get; private set; }

        public TablePickerWindow(AppDbContext db, Table currentTable, TablePickerMode mode)
        {
            InitializeComponent();

            if (mode == TablePickerMode.Move)
            {
                TxtTitle.Text    = "Masayi Tasi";
                TxtSubtitle.Text = $"{currentTable.Name} masasindaki siparisi tasimak istediginiz\nBOS masayi veya mevcut VERESİYE musterisini secin.";
            }
            else
            {
                TxtTitle.Text    = "Masa Birlestir";
                TxtSubtitle.Text = $"Hangi masanin urunlerini {currentTable.Name} masasina aktarmak istiyorsunuz?";
            }

            Loaded += async (s, e) =>
            {
                var items = new ObservableCollection<object>();

                if (mode == TablePickerMode.Move)
                {
                    // Boş masalar
                    var tables = await db.Tables
                        .Where(t => t.IsActive && t.Id != currentTable.Id && t.Status == TableStatus.Empty)
                        .OrderBy(t => t.DisplayOrder)
                        .ToListAsync();

                    foreach (var t in tables)
                        items.Add(new TablePickerTableItem { Id = t.Id, Name = t.Name });

                    // Mevcut veresiye müşterileri
                    var payments = await db.Payments
                        .Where(p => p.PaymentType == PaymentType.Veresiye && p.CustomerName != null && p.Amount > 0)
                        .ToListAsync();

                    var grouped = payments
                        .GroupBy(p => p.CustomerName!.Trim().ToLower())
                        .Where(g => !string.IsNullOrEmpty(g.Key))
                        .Select(g => new TablePickerVeresiyeItem
                        {
                            CustomerName = g.First().CustomerName!,
                            TotalAmount  = g.Sum(p => p.Amount),
                            PaymentIds   = g.Select(p => p.Id).ToList()
                        })
                        .OrderBy(v => v.CustomerName);

                    foreach (var v in grouped)
                        items.Add(v);

                    if (!items.Any())
                        TxtSubtitle.Text += "\n\nUygun bos masa veya veresiye bulunamadi.";
                }
                else
                {
                    // Birleştirme: sadece aktif masalar
                    var tables = await db.Tables
                        .Where(t => t.IsActive && t.Id != currentTable.Id && t.Status == TableStatus.Active)
                        .OrderBy(t => t.DisplayOrder)
                        .ToListAsync();

                    var activeOrders = await db.Orders
                        .Where(o => o.Status == OrderStatus.Open)
                        .Include(o => o.Items)
                        .ToListAsync();

                    foreach (var t in tables)
                    {
                        var order = activeOrders.FirstOrDefault(o => o.TableId == t.Id);
                        var total = order?.Items.Sum(i =>
                            (i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity) * i.PriceSnapshot) ?? 0;
                        items.Add(new TablePickerTableItem { Id = t.Id, Name = t.Name, CurrentTotal = total });
                    }

                    if (!items.Any())
                        TxtSubtitle.Text += "\n\nBirlesitirilecek aktif masa bulunamadi.";
                }

                IcItems.ItemsSource = items;
            };
        }

        private void BtnTableItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            if (btn.Tag is TablePickerTableItem table)
            {
                SelectedTableItem = table;
                DialogResult = true;
            }
            else if (btn.Tag is TablePickerVeresiyeItem veresiye)
            {
                SelectedVeresiyeItem = veresiye;
                DialogResult = true;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
