using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EsnafPos.Data;
using EsnafPos.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EsnafPos.ViewModels
{
    public partial class AdminViewModel : BaseViewModel
    {
        private readonly AppDbContext _db;

        // MASA
        [ObservableProperty] private ObservableCollectionEx<EsnafPos.Models.Table> _tables = new();
        [ObservableProperty] private string _newTableName = "";
        [ObservableProperty] private EsnafPos.Models.Table? _editingTable;
        [ObservableProperty] private string _editTableName = "";

        // KATEGORi
        [ObservableProperty] private ObservableCollectionEx<Category> _categories = new();
        [ObservableProperty] private string _newCategoryName = "";
        [ObservableProperty] private string _newCategoryChannel = "Masa";
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private Category? _editingCategory;
        [ObservableProperty] private string _editCategoryName = "";
        [ObservableProperty] private string _editCategoryChannel = "Masa";
        [ObservableProperty] private string _categoryErrorMessage = "";

        // URUN - Yeni ekle
        [ObservableProperty] private ObservableCollectionEx<Product> _products = new();
        [ObservableProperty] private string _newProductName = "";
        [ObservableProperty] private string _newProductPriceAz = "";
        [ObservableProperty] private string _newProductPriceTam = "";
        [ObservableProperty] private string _newProductPriceBucuk = "";
        [ObservableProperty] private string _errorMessage = "";

        // URUN - Duzenle
        [ObservableProperty] private Product? _editingProduct;
        [ObservableProperty] private string _editProductName = "";
        [ObservableProperty] private string _editProductPriceAz = "";
        [ObservableProperty] private string _editProductPriceTam = "";
        [ObservableProperty] private string _editProductPriceBucuk = "";

        // KULLANICI
        [ObservableProperty] private ObservableCollectionEx<User> _users = new();
        [ObservableProperty] private string _newUsername = "";
        [ObservableProperty] private string _newUserPin = "";
        [ObservableProperty] private string _userErrorMessage = "";
        [ObservableProperty] private User? _editingUser;
        [ObservableProperty] private string _editUsername = "";
        [ObservableProperty] private string _editUserPin = "";
        [ObservableProperty] private string _newUserRole = "Cashier";

        public AdminViewModel(AppDbContext db)
        {
            _db = db;
        }

        [RelayCommand]
        public async Task Load()
        {
            // *** Client modunda yerel DB yok, admin paneli sadece ayarlar icin kullanilabilir ***
            if (App.Client != null) return;

            IsBusy = true;
            try
            {
                var tables = await _db.Tables
                    .Where(t => t.IsActive).OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name).ToListAsync();
                Tables.Clear();
                foreach (var t in tables) Tables.Add(t);

                // *** DÜZELTME: Include ile ürünleri de çek ***
                var cats = await _db.Categories
                    .Where(c => c.IsActive)
                    .Include(c => c.Products.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder))
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();
                Categories.Clear();
                foreach (var c in cats) Categories.Add(c);

                Products.Clear();
                SelectedCategory = null;
                EditingTable = null;
                EditingProduct = null;
                EditingCategory = null;

                await LoadUsers();
            }
            finally { IsBusy = false; }
        }

        // ─── MASA ────────────────────────────────────────────

        [RelayCommand]
        private async Task AddTable()
        {
            if (string.IsNullOrWhiteSpace(NewTableName)) return;
            var maxOrder = Tables.Count > 0 ? Tables.Max(t => t.DisplayOrder) : 0;
            var table = new EsnafPos.Models.Table
            {
                Name = NewTableName.Trim(),
                Status = TableStatus.Empty,
                IsActive = true,
                DisplayOrder = maxOrder + 1
            };
            _db.Tables.Add(table);
            await _db.SaveChangesAsync();
            Tables.Add(table);
            NewTableName = "";
        }

        [RelayCommand]
        private async Task DeleteTable(EsnafPos.Models.Table? table)
        {
            if (table == null) return;
            table.IsActive = false;
            _db.Tables.Update(table);
            await _db.SaveChangesAsync();
            Tables.Remove(table);
        }

        [RelayCommand]
        private void StartEditTable(EsnafPos.Models.Table? table)
        {
            if (table == null) return;
            EditingTable = table;
            EditTableName = table.Name;
        }

        [RelayCommand]
        private async Task SaveEditTable()
        {
            if (EditingTable == null || string.IsNullOrWhiteSpace(EditTableName)) return;
            EditingTable.Name = EditTableName.Trim();
            _db.Tables.Update(EditingTable);
            await _db.SaveChangesAsync();
            EditingTable = null;
            EditTableName = "";
            await Load();
        }

        [RelayCommand]
        private void CancelEditTable()
        {
            EditingTable = null;
            EditTableName = "";
        }

        [RelayCommand]
        private void StartAddProduct(Category? category)
        {
            if (category == null) return;
            SelectedCategory     = category;
            EditingProduct       = new Product { CategoryId = category.Id };
            EditProductName      = "";
            EditProductPriceTam  = "";
            EditProductPriceAz   = "";
            EditProductPriceBucuk= "";
        }

        [RelayCommand]
        private async Task MoveTableUp(EsnafPos.Models.Table? table)
        {
            if (table == null) return;
            var list = Tables.ToList();
            int idx = list.IndexOf(table);
            if (idx <= 0) return;
            var prev = list[idx - 1];
            int tmp = table.DisplayOrder;
            table.DisplayOrder = prev.DisplayOrder;
            prev.DisplayOrder = tmp;
            _db.Tables.Update(table);
            _db.Tables.Update(prev);
            await _db.SaveChangesAsync();
            await Load();
        }

        [RelayCommand]
        private async Task MoveTableDown(EsnafPos.Models.Table? table)
        {
            if (table == null) return;
            var list = Tables.ToList();
            int idx = list.IndexOf(table);
            if (idx < 0 || idx >= list.Count - 1) return;
            var next = list[idx + 1];
            int tmp = table.DisplayOrder;
            table.DisplayOrder = next.DisplayOrder;
            next.DisplayOrder = tmp;
            _db.Tables.Update(table);
            _db.Tables.Update(next);
            await _db.SaveChangesAsync();
            await Load();
        }

        [RelayCommand]
        private async Task MoveCategoryUp(Category? category)
        {
            if (category == null) return;
            var list = Categories.ToList();
            int idx = list.IndexOf(category);
            if (idx <= 0) return;
            var prev = list[idx - 1];
            int tmp = category.DisplayOrder;
            category.DisplayOrder = prev.DisplayOrder;
            prev.DisplayOrder = tmp;
            _db.Categories.Update(category);
            _db.Categories.Update(prev);
            await _db.SaveChangesAsync();
            await Load();
        }

        [RelayCommand]
        private async Task MoveCategoryDown(Category? category)
        {
            if (category == null) return;
            var list = Categories.ToList();
            int idx = list.IndexOf(category);
            if (idx < 0 || idx >= list.Count - 1) return;
            var next = list[idx + 1];
            int tmp = category.DisplayOrder;
            category.DisplayOrder = next.DisplayOrder;
            next.DisplayOrder = tmp;
            _db.Categories.Update(category);
            _db.Categories.Update(next);
            await _db.SaveChangesAsync();
            await Load();
        }

        // ─── KATEGORi ─────────────────────────────────────────

        [RelayCommand]
        private async Task AddCategory()
        {
            CategoryErrorMessage = "";
            if (string.IsNullOrWhiteSpace(NewCategoryName)) return;
            var cat = new Category
            {
                Name = NewCategoryName.Trim(),
                Channel = string.IsNullOrWhiteSpace(NewCategoryChannel) ? "Masa" : NewCategoryChannel.Trim(),
                IsActive = true,
                DisplayOrder = Categories.Count + 1
            };
            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();
            Categories.Add(cat);
            NewCategoryName = "";
        }

        [RelayCommand]
        private void StartEditCategory(Category? category)
        {
            if (category == null) return;
            EditingCategory = category;
            EditCategoryName = category.Name;
            EditCategoryChannel = string.IsNullOrWhiteSpace(category.Channel) ? "Masa" : category.Channel;
            CategoryErrorMessage = "";
        }

        [RelayCommand]
        private async Task SaveEditCategory()
        {
            CategoryErrorMessage = "";
            if (EditingCategory == null || string.IsNullOrWhiteSpace(EditCategoryName)) return;

            EditingCategory.Name = EditCategoryName.Trim();
            EditingCategory.Channel = string.IsNullOrWhiteSpace(EditCategoryChannel) ? "Masa" : EditCategoryChannel.Trim();
            _db.Categories.Update(EditingCategory);
            await _db.SaveChangesAsync();

            EditingCategory = null;
            EditCategoryName = "";
            await Load();
        }

        [RelayCommand]
        private void CancelEditCategory()
        {
            EditingCategory = null;
            EditCategoryName = "";
            CategoryErrorMessage = "";
        }

        [RelayCommand]
        private async Task DeleteCategory(Category? category)
        {
            CategoryErrorMessage = "";
            if (category == null) return;

            var products = await _db.Products
                .Where(p => p.CategoryId == category.Id && p.IsActive)
                .ToListAsync();

            foreach (var p in products)
                p.IsActive = false;

            category.IsActive = false;
            _db.Categories.Update(category);
            await _db.SaveChangesAsync();

            if (SelectedCategory?.Id == category.Id)
            {
                SelectedCategory = null;
                Products.Clear();
            }

            await Load();
        }

        [RelayCommand]
        private async Task SelectCategoryForProducts(Category? category)
        {
            if (category == null) return;
            SelectedCategory = category;

            var products = await _db.Products
                .Where(p => p.CategoryId == category.Id && p.IsActive)
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            // ViewModel Products (urun ekleme formu icin)
            Products.Clear();
            foreach (var p in products) Products.Add(p);

            // *** DÜZELTME: Expander icin Category.Products listesini de guncelle ***
            category.Products.Clear();
            foreach (var p in products) category.Products.Add(p);
        }

        // ─── URUN ─────────────────────────────────────────────

        [RelayCommand]
        private async Task AddProduct()
        {
            ErrorMessage = "";
            if (SelectedCategory == null) { ErrorMessage = "Once bir kategori secin!"; return; }
            if (string.IsNullOrWhiteSpace(NewProductName)) { ErrorMessage = "Urun adi bos olamaz!"; return; }

            if (!TryParsePrice(NewProductPriceTam, out var priceTam))
            {
                ErrorMessage = "Tam porsiyon fiyati zorunludur!";
                return;
            }

            TryParsePrice(NewProductPriceAz, out var priceAz);
            TryParsePrice(NewProductPriceBucuk, out var priceBucuk);

            var product = new Product
            {
                Name = NewProductName.Trim(),
                Price = priceTam,
                PriceTam = priceTam,
                PriceAz = string.IsNullOrWhiteSpace(NewProductPriceAz) ? null : priceAz,
                PriceBucuk = string.IsNullOrWhiteSpace(NewProductPriceBucuk) ? null : priceBucuk,
                CategoryId = SelectedCategory.Id,
                IsActive = true,
                DisplayOrder = Products.Count + 1
            };
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // ViewModel listesi
            Products.Add(product);
            // *** DÜZELTME: Expander icin Category.Products de guncelle ***
            SelectedCategory.Products.Add(product);

            NewProductName = "";
            NewProductPriceAz = "";
            NewProductPriceTam = "";
            NewProductPriceBucuk = "";
        }

        [RelayCommand]
        private async Task DeleteProduct(Product? product)
        {
            if (product == null) return;
            product.IsActive = false;
            _db.Products.Update(product);
            await _db.SaveChangesAsync();

            // ViewModel listesi
            Products.Remove(product);
            // *** DÜZELTME: Expander icin Category.Products de guncelle ***
            var owningCategory = Categories.FirstOrDefault(c => c.Id == product.CategoryId);
            owningCategory?.Products.Remove(product);
        }

        [RelayCommand]
        private async Task MoveProductUp(Product? product)
        {
            if (product == null) return;
            var list = Products.ToList();
            int idx = list.IndexOf(product);
            if (idx <= 0) return;

            var prev = list[idx - 1];
            int tmpOrder = product.DisplayOrder;
            product.DisplayOrder = prev.DisplayOrder;
            prev.DisplayOrder = tmpOrder;

            _db.Products.Update(product);
            _db.Products.Update(prev);
            await _db.SaveChangesAsync();

            if (SelectedCategory != null)
                await SelectCategoryForProducts(SelectedCategory);
        }

        [RelayCommand]
        private async Task MoveProductDown(Product? product)
        {
            if (product == null) return;
            var list = Products.ToList();
            int idx = list.IndexOf(product);
            if (idx < 0 || idx >= list.Count - 1) return;

            var next = list[idx + 1];
            int tmpOrder = product.DisplayOrder;
            product.DisplayOrder = next.DisplayOrder;
            next.DisplayOrder = tmpOrder;

            _db.Products.Update(product);
            _db.Products.Update(next);
            await _db.SaveChangesAsync();

            if (SelectedCategory != null)
                await SelectCategoryForProducts(SelectedCategory);
        }

        [RelayCommand]
        private void StartEditProduct(Product? product)
        {
            if (product == null) return;
            EditingProduct = product;
            EditProductName = product.Name;
            EditProductPriceAz = product.PriceAz.HasValue ? product.PriceAz.Value.ToString("N2") : "";
            EditProductPriceTam = product.PriceTam.ToString("N2");
            EditProductPriceBucuk = product.PriceBucuk.HasValue ? product.PriceBucuk.Value.ToString("N2") : "";
        }

        [RelayCommand]
        private async Task SaveEditProduct()
        {
            if (EditingProduct == null || string.IsNullOrWhiteSpace(EditProductName)) return;
            if (!TryParsePrice(EditProductPriceTam, out var priceTam))
            {
                ErrorMessage = "Tam porsiyon fiyati zorunludur!";
                return;
            }

            TryParsePrice(EditProductPriceAz, out var priceAz);
            TryParsePrice(EditProductPriceBucuk, out var priceBucuk);

            EditingProduct.Name = EditProductName.Trim();
            EditingProduct.Price = priceTam;
            EditingProduct.PriceTam = priceTam;
            EditingProduct.PriceAz = string.IsNullOrWhiteSpace(EditProductPriceAz) ? null : priceAz;
            EditingProduct.PriceBucuk = string.IsNullOrWhiteSpace(EditProductPriceBucuk) ? null : priceBucuk;
            EditingProduct.UpdatedAt = DateTime.Now;

            _db.Products.Update(EditingProduct);
            await _db.SaveChangesAsync();
            EditingProduct = null;
            EditProductName = "";
            EditProductPriceAz = "";
            EditProductPriceTam = "";
            EditProductPriceBucuk = "";
            if (SelectedCategory != null)
                await SelectCategoryForProducts(SelectedCategory);
        }

        [RelayCommand]
        private void CancelEditProduct()
        {
            EditingProduct = null;
            EditProductName = "";
            EditProductPriceAz = "";
            EditProductPriceTam = "";
            EditProductPriceBucuk = "";
        }

        // ─── KULLANICI ────────────────────────────────────────

        private async Task LoadUsers()
        {
            var users = await _db.Users
                .Where(u => u.IsActive).OrderBy(u => u.Username).ToListAsync();
            Users.Clear();
            foreach (var u in users) Users.Add(u);
        }

        [RelayCommand]
        private async Task AddUser()
        {
            UserErrorMessage = "";
            if (string.IsNullOrWhiteSpace(NewUsername))
            {
                UserErrorMessage = "Kullanici adi bos olamaz!";
                return;
            }
            if (string.IsNullOrWhiteSpace(NewUserPin) || NewUserPin.Length < 4)
            {
                UserErrorMessage = "Sifre en az 4 karakter olmalidir!";
                return;
            }
            var exists = await _db.Users.AnyAsync(u =>
                u.Username == NewUsername.Trim() && u.IsActive);
            if (exists)
            {
                UserErrorMessage = "Bu kullanici adi zaten kullaniliyor!";
                return;
            }
            var user = new User
            {
                Username = NewUsername.Trim(),
                PinHash = HashPin(NewUserPin),
                Role = UserRole.Cashier,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            Users.Add(user);
            NewUsername = "";
            NewUserPin = "";
        }

        [RelayCommand]
        private void StartEditUser(User? user)
        {
            if (user == null) return;
            EditingUser = user;
            EditUsername = user.Username;
            EditUserPin = "";
            UserErrorMessage = "";
        }

        [RelayCommand]
        private async Task SaveEditUser()
        {
            UserErrorMessage = "";
            if (EditingUser == null) return;
            if (string.IsNullOrWhiteSpace(EditUsername))
            {
                UserErrorMessage = "Kullanici adi bos olamaz!";
                return;
            }
            var exists = await _db.Users.AnyAsync(u =>
                u.Username == EditUsername.Trim() && u.IsActive && u.Id != EditingUser.Id);
            if (exists)
            {
                UserErrorMessage = "Bu kullanici adi zaten kullaniliyor!";
                return;
            }
            EditingUser.Username = EditUsername.Trim();
            if (!string.IsNullOrWhiteSpace(EditUserPin))
            {
                if (EditUserPin.Length < 4)
                {
                    UserErrorMessage = "Sifre en az 4 karakter olmalidir!";
                    return;
                }
                EditingUser.PinHash = HashPin(EditUserPin);
            }
            _db.Users.Update(EditingUser);
            await _db.SaveChangesAsync();
            EditingUser = null;
            EditUsername = "";
            EditUserPin = "";
            await LoadUsers();
        }

        [RelayCommand]
        private void CancelEditUser()
        {
            EditingUser = null;
            EditUsername = "";
            EditUserPin = "";
            UserErrorMessage = "";
        }

        [RelayCommand]
        private async Task DeleteUser(User? user)
        {
            if (user == null) return;
            if (user.Role == UserRole.Admin)
            {
                UserErrorMessage = "Admin kullanici silinemez!";
                return;
            }
            user.IsActive = false;
            _db.Users.Update(user);
            await _db.SaveChangesAsync();
            Users.Remove(user);
        }

        // ─── VERESİYE YÖNETİMİ ───────────────────────────────────

        [ObservableProperty] private System.Collections.ObjectModel.ObservableCollection<VeresiyeAdminEntry> _veresiyeEntries = new();
        [ObservableProperty] private VeresiyeAdminEntry? _editingVeresiye;
        [ObservableProperty] private string _editVeresiyeName = "";

        [RelayCommand]
        public async Task LoadVeresiye()
        {
            var payments = await _db.Payments
                .Where(p => p.PaymentType == PaymentType.Veresiye && p.CustomerName != null)
                .OrderBy(p => p.CustomerName)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();

            var orderIds = payments.Select(p => p.OrderId).Distinct().ToList();
            var orders = await _db.Orders
                .Where(o => orderIds.Contains(o.Id))
                .ToListAsync();

            VeresiyeEntries.Clear();
            foreach (var p in payments)
            {
                var order = orders.FirstOrDefault(o => o.Id == p.OrderId);
                VeresiyeEntries.Add(new VeresiyeAdminEntry
                {
                    PaymentId    = p.Id,
                    CustomerName = p.CustomerName!,
                    Amount       = p.Amount,
                    OrderId      = p.OrderId,
                    TableName    = order?.TableNameSnapshot ?? "",
                    CreatedAt    = p.CreatedAt
                });
            }
        }

        [RelayCommand]
        private void StartEditVeresiye(VeresiyeAdminEntry? entry)
        {
            if (entry == null) return;
            EditingVeresiye = entry;
            EditVeresiyeName = entry.CustomerName;
        }

        [RelayCommand]
        private void CancelEditVeresiye()
        {
            EditingVeresiye = null;
            EditVeresiyeName = "";
        }

        [RelayCommand]
        private async Task SaveVeresiyeName()
        {
            if (EditingVeresiye == null || string.IsNullOrWhiteSpace(EditVeresiyeName)) return;
            var payment = await _db.Payments.FindAsync(EditingVeresiye.PaymentId);
            if (payment != null)
            {
                payment.CustomerName = EditVeresiyeName.Trim();
                _db.Payments.Update(payment);
                await _db.SaveChangesAsync();
            }
            EditingVeresiye = null;
            EditVeresiyeName = "";
            await LoadVeresiye();
        }

        [RelayCommand]
        private async Task DeleteVeresiyeEntry(VeresiyeAdminEntry? entry)
        {
            if (entry == null) return;
            var payment = await _db.Payments.FindAsync(entry.PaymentId);
            if (payment == null) return;

            var items = await _db.OrderItems
                .Where(i => i.OrderId == entry.OrderId && i.VeresiyeQuantity > 0)
                .ToListAsync();
            foreach (var item in items)
            {
                item.VeresiyeQuantity = 0;
                _db.OrderItems.Update(item);
            }

            var order = await _db.Orders.FindAsync(entry.OrderId);
            if (order != null && order.Status == OrderStatus.Veresiye)
            {
                order.Status = OrderStatus.Open;
                var table = await _db.Tables.FindAsync(order.TableId);
                if (table != null) { table.Status = TableStatus.Active; _db.Tables.Update(table); }
            }

            _db.Payments.Remove(payment);
            await _db.SaveChangesAsync();
            await LoadVeresiye();
        }

        [RelayCommand]
        private async Task TestPrint()
        {
            await Task.CompletedTask;
        }

        // ─── YARDIMCI ─────────────────────────────────────────

        private static bool TryParsePrice(string input, out decimal result)
        {
            return decimal.TryParse(
                input.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        private static string HashPin(string pin)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
            return Convert.ToHexString(bytes).ToLower();
        }
    }

    public class VeresiyeAdminEntry
    {
        public int PaymentId { get; set; }
        public string CustomerName { get; set; } = "";
        public decimal Amount { get; set; }
        public int OrderId { get; set; }
        public string TableName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class ObservableCollectionEx<T> : System.Collections.ObjectModel.ObservableCollection<T>
    {
        public new void Clear() => base.ClearItems();
    }
}
