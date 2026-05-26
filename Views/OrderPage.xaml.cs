using EsnafPos.ViewModels;
using EsnafPos.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EsnafPos.Views
{
    public enum PortionMode { Tam, Az, Bucuk }

    public partial class OrderPage : Page
    {
        private readonly OrderViewModel _vm;
        private EsnafPos.Models.Table _table;
        private bool _paymentCompleted = false;
        private readonly bool _isReturn;

        public OrderPage(OrderViewModel vm, EsnafPos.Models.Table table, bool isReturn = false)
        {
            InitializeComponent();
            _vm       = vm;
            _table    = table;
            _isReturn = isReturn;
            DataContext = vm;

            _vm.ReasonRequested += (productName, qty) =>
            {
                var dialog = new ReasonDialog(productName, qty);
                dialog.Owner = Window.GetWindow(this);
                var result = dialog.ShowDialog();
                return result == true ? dialog.SelectedReason : null;
            };

            _vm.PaymentRequested += OnPaymentRequested;
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_vm.CurrentOrder))
                {
                    BtnMoveTable.Visibility  = _vm.CurrentOrder != null ? Visibility.Visible : Visibility.Collapsed;
                    BtnMergeTable.Visibility = _vm.CurrentOrder != null ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            _vm.Channels.CollectionChanged   += (s, e) => BuildChannelButtons();
            _vm.Categories.CollectionChanged += (s, e) => Dispatcher.Invoke(BuildCategoryButtons);
            _vm.Products.CollectionChanged   += (s, e) =>
            {
                if (!_isDragging) Dispatcher.Invoke(BuildProductButtons);
            };

            Loaded += async (s, e) =>
            {
                InitEditModeButton();
                // Sadece admin gorebilir
                var session = App.Services.GetRequiredService<SessionService>();
                BtnEditMode.Visibility = session.IsAdmin
                    ? Visibility.Visible : Visibility.Collapsed;
                await _vm.LoadForTable(_table, _isReturn);
                // Siparis yoksa tasi/birlestir gizle
                BtnMoveTable.Visibility  = _vm.CurrentOrder != null ? Visibility.Visible : Visibility.Collapsed;
                BtnMergeTable.Visibility = _vm.CurrentOrder != null ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        private async void OnPaymentRequested()
        {
            if (_vm.CurrentOrder == null || _vm.CurrentTable == null) return;

            var paymentVm = App.Services.GetRequiredService<PaymentViewModel>();
            await paymentVm.Load(_vm.CurrentOrder, _vm.CurrentTable);
            _paymentCompleted = false;
            paymentVm.PaymentCompleted += () => _paymentCompleted = true;

            var paymentWindow = new PaymentWindow(paymentVm);
            paymentWindow.Owner = Window.GetWindow(this);
            paymentWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            paymentWindow.ShowDialog();

            if (_paymentCompleted)
            {
                _paymentCompleted = false;
                if (paymentVm.WasFullyPaid)
                    NavigationService?.GoBack();
                else
                    await _vm.LoadForTable(_table, true); // Kısmi ödeme - masada kal
            }
        }

        // ─── KANAL BUTONLARI ──────────────────────────────────

        private void BuildChannelButtons()
        {
            PanelChannels.Children.Clear();
            foreach (var channel in _vm.Channels)
            {
                var btn = new Button
                {
                    Height          = 46,
                    Padding         = new Thickness(28, 0, 28, 0),
                    Margin          = new Thickness(0, 0, 10, 0),
                    BorderThickness = new Thickness(0),
                    Tag             = channel,
                    FontSize        = 16,
                    FontWeight      = FontWeights.Bold,
                    Cursor          = Cursors.Hand
                };
                btn.Style = (Style)FindResource("BaseButton");
                btn.Click += BtnChannel_Click;
                PanelChannels.Children.Add(btn);
            }
            UpdateChannelButtonColors();
        }

        private void UpdateChannelButtonColors()
        {
            var activeBrush   = (SolidColorBrush)FindResource("PrimaryBrush");
            var inactiveBrush = new SolidColorBrush(Colors.White);
            var activeText    = new SolidColorBrush(Colors.White);
            var inactiveText  = (SolidColorBrush)FindResource("PrimaryBrush");

            foreach (Button btn in PanelChannels.Children)
            {
                bool isActive = btn.Tag?.ToString() == _vm.SelectedChannel;
                btn.Background = isActive ? activeBrush : inactiveBrush;
                btn.Content = new TextBlock
                {
                    Text       = btn.Tag?.ToString(),
                    FontSize   = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = isActive ? activeText : inactiveText
                };
            }
        }

        private async void BtnChannel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string channel)
            {
                await _vm.SelectChannel(channel);
                UpdateChannelButtonColors();
            }
        }

        // ─── KATEGORi BUTONLARI ───────────────────────────────

        private static readonly string[] CategoryColors =
        {
            "#27AE60", "#2980B9", "#8E44AD",
            "#E67E22", "#16A085", "#C0392B",
        };

        private void BuildCategoryButtons()
        {
            PanelCategories.Children.Clear();
            int idx = 0;
            foreach (var cat in _vm.Categories)
            {
                var color = CategoryColors[idx % CategoryColors.Length];
                var btn = new Button
                {
                    MinHeight       = 48,
                    Padding         = new Thickness(8, 10, 8, 10),
                    Margin          = new Thickness(0, 0, 0, 6),
                    BorderThickness = new Thickness(0),
                    Background      = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                    Tag             = cat,
                    Cursor          = Cursors.Hand
                };
                btn.Style = (Style)FindResource("BaseButton");
                btn.Click += BtnCategory_Click;
                PanelCategories.Children.Add(btn);
                idx++;
            }
            UpdateCategoryButtonColors();
        }

        private void UpdateCategoryButtonColors()
        {
            int idx = 0;
            foreach (Button btn in PanelCategories.Children)
            {
                if (btn.Tag is not EsnafPos.Models.Category cat) { idx++; continue; }
                bool isSelected = _vm.SelectedCategory?.Id == cat.Id;

                var baseColor = (Color)ColorConverter.ConvertFromString(
                    CategoryColors[idx % CategoryColors.Length]);
                var bgColor = isSelected ? DarkenColor(baseColor, 0.75f) : baseColor;

                btn.Background      = new SolidColorBrush(bgColor);
                btn.BorderThickness = isSelected ? new Thickness(0, 0, 4, 0) : new Thickness(0);
                btn.BorderBrush     = isSelected ? Brushes.White : null;
                btn.Content = new TextBlock
                {
                    Text          = cat.Name,
                    FontSize      = 14,
                    FontWeight    = isSelected ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground    = Brushes.White,
                    TextWrapping  = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                };
                idx++;
            }
        }

        private static Color DarkenColor(Color c, float f)
            => Color.FromRgb((byte)(c.R * f), (byte)(c.G * f), (byte)(c.B * f));

        private async void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EsnafPos.Models.Category cat)
            {
                await _vm.SelectCategoryCommand.ExecuteAsync(cat);
                UpdateCategoryButtonColors();
            }
        }

        // ─── URUN BUTONLARI ───────────────────────────────────

        private void BuildProductButtons()
        {
            PanelProducts.Children.Clear();
            foreach (var product in _vm.Products)
            {
                var btn = CreateProductButton(product);
                PanelProducts.Children.Add(btn);
            }
            if (_isEditMode) ApplyEditModeStyle(true);
        }

        private Button CreateProductButton(EsnafPos.Models.Product product)
        {
            var btn = new Button
            {
                Width           = 170,
                Height          = 70,
                Margin          = new Thickness(4),
                Background      = Brushes.White,
                BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD5, 0xD8, 0xDC)),
                BorderThickness = new Thickness(1.5),
                Tag             = product,
                Cursor          = Cursors.Hand,
                Template        = (ControlTemplate)FindResource("ProductButtonTemplate")
            };
            btn.Content = new TextBlock
            {
                Text          = product.Name,
                FontSize      = 14,
                FontWeight    = FontWeights.SemiBold,
                Foreground    = (SolidColorBrush)FindResource("PrimaryBrush"),
                TextWrapping  = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin        = new Thickness(8, 0, 8, 0)
            };
            btn.Click += BtnProduct_Click;
            return btn;
        }

        private async void BtnProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode) return;
            if (sender is Button btn && btn.Tag is EsnafPos.Models.Product product)
            {
                await _vm.AddProductWithMode(product, _portionMode);
                // Ürün eklendikten sonra modu sıfırla
                _portionMode = PortionMode.Tam;
                UpdateModeButtons();
            }
        }

        // ─── EDIT MODE ────────────────────────────────────────

        private bool _isEditMode = false;
        private PortionMode _portionMode = PortionMode.Tam;

        private void InitEditModeButton()
        {
            SetEditModeButton(false);
        }

        private void BtnEditMode_Click(object sender, RoutedEventArgs e)
        {
            _isEditMode = !_isEditMode;
            SetEditModeButton(_isEditMode);

            if (_isEditMode)
            {
                ApplyEditModeStyle(true);
                ShakeProducts();
            }
            else
            {
                ApplyEditModeStyle(false);
                _ = SaveProductOrderAsync();
            }
        }

        private void SetEditModeButton(bool active)
        {
            // Her iki durumda da turuncu — aktifse koyu turuncu
            BtnEditMode.Background = active
                ? new SolidColorBrush(Color.FromRgb(0xCA, 0x6F, 0x1E))  // koyu turuncu
                : new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)); // turuncu

            BtnEditMode.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius   = 8,
                ShadowDepth  = 2,
                Color        = Color.FromRgb(0xE6, 0x7E, 0x22),
                Opacity      = active ? 0.6 : 0.3
            };

            BtnEditMode.Content = new Border
            {
                Padding = new Thickness(10, 6, 10, 6),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock
                        {
                            Text      = active ? "✓" : "⠿",
                            FontSize  = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            Margin    = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text      = active ? "Bitti" : "Duzenle",
                            FontSize  = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }

        private void ApplyEditModeStyle(bool editMode)
        {
            foreach (UIElement child in PanelProducts.Children)
            {
                if (child is not Button btn) continue;

                if (editMode)
                {
                    btn.BorderBrush     = new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22));
                    btn.BorderThickness = new Thickness(2);
                    btn.Cursor          = Cursors.SizeAll;
                    btn.Click          -= BtnProduct_Click;
                }
                else
                {
                    btn.BorderBrush     = new SolidColorBrush(Color.FromRgb(0xD5, 0xD8, 0xDC));
                    btn.BorderThickness = new Thickness(1.5);
                    btn.Cursor          = Cursors.Hand;
                    btn.RenderTransform = null;
                    btn.Opacity         = 1.0;
                    btn.Click          += BtnProduct_Click;
                }
            }
        }

        private void ShakeProducts()
        {
            int i = 0;
            foreach (UIElement child in PanelProducts.Children)
            {
                if (child is not Button btn) { i++; continue; }
                var transform = new TranslateTransform();
                btn.RenderTransform       = transform;
                btn.RenderTransformOrigin = new Point(0.5, 0.5);

                var anim = new DoubleAnimation
                {
                    From              = -4,
                    To                = 4,
                    Duration          = TimeSpan.FromMilliseconds(65),
                    AutoReverse       = true,
                    RepeatBehavior    = new RepeatBehavior(3),
                    BeginTime         = TimeSpan.FromMilliseconds(i * 18),
                    FillBehavior      = FillBehavior.Stop
                };
                transform.BeginAnimation(TranslateTransform.XProperty, anim);
                i++;
            }
        }

        // ─── PORSIYON MODU ────────────────────────────────────

        private void BtnModeAz_Click(object sender, RoutedEventArgs e)
        {
            _portionMode = _portionMode == PortionMode.Az ? PortionMode.Tam : PortionMode.Az;
            UpdateModeButtons();
        }

        private void BtnModeBucuk_Click(object sender, RoutedEventArgs e)
        {
            _portionMode = _portionMode == PortionMode.Bucuk ? PortionMode.Tam : PortionMode.Bucuk;
            UpdateModeButtons();
        }

        private void UpdateModeButtons()
        {
            // Az butonu: normal=turuncu, aktif=koyu turuncu + yazı sarı
            bool azActive = _portionMode == PortionMode.Az;
            BtnModeAz.Background = azActive
                ? new SolidColorBrush(Color.FromRgb(0xA0, 0x40, 0x00)) // koyu turuncu - aktif
                : new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)); // normal turuncu
            TxtModeAz.Foreground = azActive
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0x82)) // sarımsı - aktif
                : Brushes.White;
            TxtModeAz.Text = azActive ? "✓ Az" : "Az";

            // 1.5 butonu: normal=mavi, aktif=koyu mavi + yazı açık mavi
            bool bucukActive = _portionMode == PortionMode.Bucuk;
            BtnModeBucuk.Background = bucukActive
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x52, 0x76)) // koyu mavi - aktif
                : new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9)); // normal mavi
            TxtModeBucuk.Foreground = bucukActive
                ? new SolidColorBrush(Color.FromRgb(0xAE, 0xD6, 0xF1)) // açık mavi - aktif
                : Brushes.White;
            TxtModeBucuk.Text = bucukActive ? "✓ 1.5 Porsiyon" : "1.5 Porsiyon";
        }

        // ─── DRAG & DROP ──────────────────────────────────────

        private bool _isDragging    = false;
        private bool _dragStarted   = false;
        private Button? _draggedButton = null;
        private int _draggedIndex   = -1;
        private Border? _ghost      = null;
        private Point _mouseDownPos;
        private Point _dragOffset;
        private const double DragThreshold = 10.0;

        // MOUSE
        private void PanelProducts_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;
            _mouseDownPos = e.GetPosition(PanelProducts);
            var btn = FindParentButton(PanelProducts.InputHitTest(_mouseDownPos) as DependencyObject);
            if (btn == null) return;
            _draggedButton = btn;
            _draggedIndex  = PanelProducts.Children.IndexOf(btn);
            _dragStarted   = false;
            Mouse.Capture(PanelProducts);
            e.Handled = true;
        }

        private void PanelProducts_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEditMode || _draggedButton == null) return;
            var panelPos = e.GetPosition(PanelProducts);
            HandleDragMove(panelPos, e.GetPosition(DragCanvas));
        }

        private void PanelProducts_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isEditMode) return;
            Mouse.Capture(null);
            HandleDragEnd();
        }

        // TOUCH
        private void PanelProducts_TouchDown(object sender, TouchEventArgs e)
        {
            if (!_isEditMode) return;
            var pos = e.GetTouchPoint(PanelProducts).Position;
            var btn = FindParentButton(PanelProducts.InputHitTest(pos) as DependencyObject);
            if (btn == null) return;
            _draggedButton = btn;
            _draggedIndex  = PanelProducts.Children.IndexOf(btn);
            _mouseDownPos  = pos;
            _dragStarted   = false;
            e.TouchDevice.Capture(PanelProducts);
            e.Handled = true;
        }

        private void PanelProducts_TouchMove(object sender, TouchEventArgs e)
        {
            if (!_isEditMode || _draggedButton == null) return;
            HandleDragMove(
                e.GetTouchPoint(PanelProducts).Position,
                e.GetTouchPoint(DragCanvas).Position);
            e.Handled = true;
        }

        private void PanelProducts_TouchUp(object sender, TouchEventArgs e)
        {
            if (!_isEditMode) return;
            e.TouchDevice.Capture(null);
            HandleDragEnd();
            e.Handled = true;
        }

        // ORTAK DRAG LOJiGi
        private void HandleDragMove(Point panelPos, Point canvasPos)
        {
            if (!_dragStarted)
            {
                var diff = panelPos - _mouseDownPos;
                if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold) return;
                _dragStarted = true;
                _isDragging  = true;
                StartDragGhost(canvasPos);
            }

            if (!_isDragging || _ghost == null) return;

            Canvas.SetLeft(_ghost, canvasPos.X - _dragOffset.X);
            Canvas.SetTop(_ghost, canvasPos.Y - _dragOffset.Y);

            var hoverIdx = GetHoverIndex(panelPos);
            if (hoverIdx >= 0 && hoverIdx != _draggedIndex)
                MoveButtonTo(_draggedIndex, hoverIdx);
        }

        private void HandleDragEnd()
        {
            if (_isDragging)
            {
                DragCanvas.Children.Remove(_ghost);
                _ghost = null;
                if (_draggedButton != null) _draggedButton.Opacity = 1.0;
            }
            _draggedButton = null;
            _draggedIndex  = -1;
            _isDragging    = false;
            _dragStarted   = false;
        }

        private void StartDragGhost(Point canvasPos)
        {
            var product = _draggedButton!.Tag as EsnafPos.Models.Product;

            _ghost = new Border
            {
                Width           = _draggedButton.ActualWidth,
                Height          = _draggedButton.ActualHeight,
                CornerRadius    = new CornerRadius(10),
                Background      = new SolidColorBrush(Color.FromArgb(220, 0xE6, 0x7E, 0x22)),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text              = product?.Name ?? "",
                    FontSize          = 14,
                    FontWeight        = FontWeights.Bold,
                    Foreground        = Brushes.White,
                    TextWrapping      = TextWrapping.Wrap,
                    TextAlignment     = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Margin            = new Thickness(8, 0, 8, 0)
                }
            };

            _dragOffset = new Point(
                _draggedButton.ActualWidth / 2,
                _draggedButton.ActualHeight / 2);

            Canvas.SetLeft(_ghost, canvasPos.X - _dragOffset.X);
            Canvas.SetTop(_ghost, canvasPos.Y - _dragOffset.Y);
            DragCanvas.Children.Add(_ghost);

            _draggedButton.Opacity = 0.25;
        }

        private int GetHoverIndex(Point panelPos)
        {
            var children = PanelProducts.Children.OfType<Button>().ToList();
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i] == _draggedButton) continue;
                var pos  = children[i].TranslatePoint(new Point(0, 0), PanelProducts);
                var rect = new Rect(pos.X, pos.Y,
                    children[i].ActualWidth, children[i].ActualHeight);
                if (rect.Contains(panelPos)) return i;
            }
            return -1;
        }

        private void MoveButtonTo(int from, int to)
        {
            if (from < 0 || from >= PanelProducts.Children.Count) return;
            if (to   < 0 || to   >= PanelProducts.Children.Count) return;
            var btn = PanelProducts.Children[from];
            PanelProducts.Children.RemoveAt(from);
            PanelProducts.Children.Insert(to, btn);
            _draggedIndex = to;
        }

        private static Button? FindParentButton(DependencyObject? el)
        {
            while (el != null)
            {
                if (el is Button b) return b;
                el = VisualTreeHelper.GetParent(el);
            }
            return null;
        }

        private async Task SaveProductOrderAsync()
        {
            var products = PanelProducts.Children
                .OfType<Button>()
                .Where(b => b.Tag is EsnafPos.Models.Product)
                .Select(b => (EsnafPos.Models.Product)b.Tag!)
                .ToList();

            if (products.Count > 0)
                await _vm.SaveProductOrderAsync(products);
        }

        // ─── MASA TAŞI + BİRLEŞTİR ──────────────────────────────

        private async void BtnMoveTable_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentOrder == null)
            {
                MessageBox.Show("Tasinacak siparis yok.", "Uyari",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var db = App.Services.GetRequiredService<EsnafPos.Data.AppDbContext>();
            var picker = new TablePickerWindow(db, _table, TablePickerMode.Move)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (picker.ShowDialog() != true) return;

            if (picker.SelectedTableItem != null)
            {
                // Boş masaya taşı
                var targetTable = await db.Tables.FindAsync(picker.SelectedTableItem.Id);
                if (targetTable == null) return;

                var error = await _vm.MoveToTable(targetTable);
                if (error != null)
                {
                    MessageBox.Show(error, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _table = targetTable;
                await _vm.LoadForTable(_table, true);
                BtnMoveTable.Visibility  = Visibility.Visible;
                BtnMergeTable.Visibility = Visibility.Visible;
            }
            else if (picker.SelectedVeresiyeItem != null)
            {
                // Mevcut veresiye müşterisine taşı
                var result = MessageBox.Show(
                    $"Masadaki tum urunler '{picker.SelectedVeresiyeItem.CustomerName}' musterisinin veresiyesine eklenecek.\nMasa kapanacak.\n\nDevam etmek istiyor musunuz?",
                    "Veresiyeye Tasi",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var error = await _vm.MoveToVeresiye(picker.SelectedVeresiyeItem.CustomerName);
                if (error != null)
                {
                    MessageBox.Show(error, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // Sipariş kapandı, masalar ekranına dön
                NavigationService?.GoBack();
            }
        }

        private async void BtnMergeTable_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CurrentOrder == null)
            {
                MessageBox.Show("Bu masada aktif siparis yok.", "Uyari",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var db = App.Services.GetRequiredService<EsnafPos.Data.AppDbContext>();
            var picker = new TablePickerWindow(db, _table, TablePickerMode.Merge)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (picker.ShowDialog() != true || picker.SelectedTableItem == null) return;

            var sourceTable = await db.Tables.FindAsync(picker.SelectedTableItem.Id);
            if (sourceTable == null) return;

            var result = MessageBox.Show(
                $"{sourceTable.Name} masasindaki urunler bu masaya aktarilacak.\n{sourceTable.Name} masasi bosalacak.\n\nDevam etmek istiyor musunuz?",
                "Masa Birlestir",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            var error = await _vm.MergeFromTable(sourceTable);
            if (error != null)
                MessageBox.Show(error, "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ─── GERI + DIGER ─────────────────────────────────────

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
                NavigationService.GoBack();
        }
    }
}
