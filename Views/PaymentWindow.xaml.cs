using EsnafPos.Models;
using EsnafPos.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace EsnafPos.Views
{
    public class ConsumedItemSnapshot
    {
        public int OrderItemId { get; set; }
        public int Quantity    { get; set; }
    }

    public class PaymentEntry
    {
        public PaymentType PaymentType  { get; set; }
        public decimal     Amount       { get; set; }
        public string?     CustomerName { get; set; }
        public List<ConsumedItemSnapshot> ConsumedItems { get; set; } = new();

        public string PaymentTypeDisplay => PaymentType switch
        {
            PaymentType.Cash       => "Nakit",
            PaymentType.CardDebit  => "Banka Karti",
            PaymentType.CardCredit => "Kredi Karti",
            PaymentType.Veresiye   => "Veresiye",
            _ => ""
        };
    }

    public partial class PaymentWindow : Window
    {
        private readonly PaymentViewModel _vm;
        private readonly ObservableCollection<PaymentEntry> _payments = new();

        public PaymentWindow(PaymentViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            RefreshItemList();
            IcPayments.ItemsSource = _payments;

            _payments.CollectionChanged += (s, e) =>
            {
                TxtNoPayments.Visibility = _payments.Any()
                    ? Visibility.Collapsed : Visibility.Visible;
                UpdateButtons();
            };
        }

        private void RefreshItemList()
        {
            IcItems.ItemsSource = _vm.ItemRows
                .Where(r => r.RemainingQuantity > 0)
                .ToList();
        }

        private void UpdateButtons()
        {
            bool hasPay  = _payments.Any();
            bool allPaid = _vm.RemainingAmount <= 0.01m;
            BtnComplete.Visibility     = (hasPay && allPaid)  ? Visibility.Visible : Visibility.Collapsed;
            BtnSaveAndClose.Visibility = (hasPay && !allPaid) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            BorderError.Visibility = Visibility.Visible;
        }
        private void HideError() => BorderError.Visibility = Visibility.Collapsed;

        // ─── TUMU SEC ────────────────────────────────────────
        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _vm.ItemRows.Where(r => r.RemainingQuantity > 0))
                row.SelectedQuantity = row.RemainingQuantity;
            _vm.RecalcSelectedTotal();
        }

        // ─── URUN +/- ────────────────────────────────────────
        private void BtnSelectAllRemaining_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _vm.ItemRows.Where(r => r.RemainingQuantity > 0))
                row.SelectedQuantity = row.RemainingQuantity;
            _vm.RecalcSelectedTotal();
        }

        private void BtnIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PaymentItemRow row)
            {
                row.IncreaseSelected();
                _vm.RecalcSelectedTotal();
            }
        }

        private void BtnDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PaymentItemRow row)
            {
                row.DecreaseSelected();
                _vm.RecalcSelectedTotal();
            }
        }

        // ─── ODEME YONTEMLERI ─────────────────────────────────
        private void BtnPayCash_Click(object sender, RoutedEventArgs e)
            => Pay(PaymentType.Cash);

        private void BtnPayDebit_Click(object sender, RoutedEventArgs e)
            => Pay(PaymentType.CardDebit);

        private void BtnPayCredit_Click(object sender, RoutedEventArgs e)
            => Pay(PaymentType.CardCredit);

        private void BtnPayVeresiye_Click(object sender, RoutedEventArgs e)
        {
            PanelVeresiyeName.Visibility = Visibility.Visible;
            TxtVeresiyeName.Focus();
        }

        private void BtnConfirmVeresiye_Click(object sender, RoutedEventArgs e)
            => Pay(PaymentType.Veresiye);

        private void TxtVeresiyeName_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                Pay(PaymentType.Veresiye);
        }

        private void Pay(PaymentType type, string? customerName = null)
        {
            HideError();

            if (type == PaymentType.Veresiye)
            {
                customerName = TxtVeresiyeName.Text.Trim();
                if (string.IsNullOrEmpty(customerName))
                {
                    ShowError("Veresiye için müşteri adı giriniz!");
                    return;
                }
            }

            if (_vm.SelectedItemsTotal <= 0)
            {
                ShowError("Lütfen önce ürün seçin!");
                return;
            }

            var amount = _vm.SelectedItemsTotal;
            if (amount > _vm.RemainingAmount + 0.01m)
                amount = _vm.RemainingAmount;

            // Secilen urunleri tüket — her urun kendi miktarinca duser
            var consumed = _vm.ItemRows
                .Where(r => r.SelectedQuantity > 0)
                .Select(r => new ConsumedItemSnapshot
                {
                    OrderItemId = r.OrderItemId,
                    Quantity    = r.SelectedQuantity
                })
                .ToList();

            _payments.Add(new PaymentEntry
            {
                PaymentType   = type,
                Amount        = amount,
                CustomerName  = customerName,
                ConsumedItems = consumed
            });

            _vm.RemainingAmount -= amount;
            if (_vm.RemainingAmount < 0) _vm.RemainingAmount = 0;

            _vm.ConsumeSelected();
            RefreshItemList();
            UpdateButtons();

            TxtVeresiyeName.Text = "";
            PanelVeresiyeName.Visibility = Visibility.Collapsed;
        }

        // ─── ODEME SIL ────────────────────────────────────────
        private void BtnRemovePayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not PaymentEntry entry) return;

            _vm.RemainingAmount += entry.Amount;

            foreach (var c in entry.ConsumedItems)
            {
                var row = _vm.ItemRows.FirstOrDefault(r => r.OrderItemId == c.OrderItemId);
                if (row != null)
                {
                    row.RemainingQuantity += c.Quantity;
                    // Eger urun listede yoksa (RemainingQuantity 0'dan fazla oldu) yenile
                }
            }

            _payments.Remove(entry);
            RefreshItemList();
            UpdateButtons();
        }

        // ─── ESKi ODEMEYi iPTAL ET ───────────────────────────
        private async void BtnUndoExisting_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not EsnafPos.Models.Payment payment) return;

            var result = System.Windows.MessageBox.Show(
$"{payment.PaymentType} - {payment.Amount:N2} TL ödemeyi iptal etmek istiyor musunuz?\n\nUYARI: Tüm ürün ödemeleri sıfırlanacak, yeniden ödeme yapmanız gerekecek.",
                "Ödeme İptali",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            await _vm.UndoExistingPayment(payment);
            RefreshItemList();
            UpdateButtons();
        }

        // ─── KAYDET VE KAPAT ─────────────────────────────────
        private async void BtnSaveAndClose_Click(object sender, RoutedEventArgs e)
        {
            await _vm.CompletePaymentWithEntries(_payments.ToList(), false);
            DialogResult = true;
            Close();
        }

        // ─── TAMAMLA ─────────────────────────────────────────
        private async void BtnComplete_Click(object sender, RoutedEventArgs e)
        {
            await _vm.CompletePaymentWithEntries(_payments.ToList(), false);
            DialogResult = true;
            Close();
        }

        // ─── iPTAL ───────────────────────────────────────────
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
