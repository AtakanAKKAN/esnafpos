using EsnafPos.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace EsnafPos.Network
{
    /// <summary>
    /// İstemci modunda sunucuya HTTP istekleri gönderir.
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private bool _isConnected = false;

        public bool IsConnected => _isConnected;

        public ApiClient(string serverIp, int port, string username, string password)
        {
            _baseUrl = $"http://{serverIp}:{port}";
            _http    = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            _http.DefaultRequestHeaders.Add("X-Api-Key",
                ApiServer.BuildApiKey(username, password));
        }

        // ─── BAĞLANTI TEST ───────────────────────────────────
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/api/tables");
                _isConnected = resp.IsSuccessStatusCode;
                return _isConnected;
            }
            catch { _isConnected = false; return false; }
        }

        // ─── LOGIN ───────────────────────────────────────────
        public async Task<LoginResponseDto?> LoginAsync(string username, string pinHash)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/login",
                    new { Username = username, PinHash = pinHash });
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
            }
            catch { return null; }
        }

        public async Task<List<UserDto>> GetUsersAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<UserDto>>($"{_baseUrl}/api/users");
                return result ?? new();
            }
            catch { return new(); }
        }

        // ─── TABLES ──────────────────────────────────────────
        public async Task<List<TableDto>> GetTablesAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<TableDto>>($"{_baseUrl}/api/tables");
                return result ?? new();
            }
            catch { return new(); }
        }

        // ─── ORDERS ──────────────────────────────────────────
        public async Task<OrderDto?> GetOrderForTableAsync(int tableId)
        {
            try
            {
                var resp = await _http.GetAsync($"{_baseUrl}/api/orders/{tableId}");
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<OrderDto>();
            }
            catch { return null; }
        }

        public async Task<List<OrderItemDto>> GetOrderItemsAsync(int orderId)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<OrderItemDto>>(
                    $"{_baseUrl}/api/orderitems/{orderId}");
                return result ?? new();
            }
            catch { return new(); }
        }

        // ─── ADD PRODUCT ─────────────────────────────────────
        public async Task<int?> AddProductAsync(int tableId, AddProductRequest req)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync(
                    $"{_baseUrl}/api/orders/{tableId}/addproduct", req);
                if (!resp.IsSuccessStatusCode) return null;
                var result = await resp.Content.ReadFromJsonAsync<AddProductResponse>();
                return result?.OrderId;
            }
            catch { return null; }
        }

        // ─── ADD QUANTITY / REMOVE ────────────────────────────
        public async Task<bool> AddQuantityAsync(int itemId)
        {
            try
            {
                var resp = await _http.PostAsync(
                    $"{_baseUrl}/api/orderitems/{itemId}/addquantity", null);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> RemoveItemAsync(int itemId)
        {
            try
            {
                var resp = await _http.PostAsync(
                    $"{_baseUrl}/api/orderitems/{itemId}/remove", null);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ─── PAYMENTS ────────────────────────────────────────
        public async Task<List<PaymentDto>> GetPaymentsAsync(int orderId)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<PaymentDto>>(
                    $"{_baseUrl}/api/payments/{orderId}");
                return result ?? new();
            }
            catch { return new(); }
        }

        public async Task<CompletePaymentResponse?> CompletePaymentAsync(CompletePaymentRequest req)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/payments", req);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<CompletePaymentResponse>();
            }
            catch { return null; }
        }

        // ─── CATEGORIES + PRODUCTS ────────────────────────────
        public async Task<List<Category>> GetCategoriesAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<Category>>($"{_baseUrl}/api/categories");
                return result ?? new();
            }
            catch { return new(); }
        }

        public async Task<List<Product>> GetProductsAsync(int categoryId)
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<Product>>(
                    $"{_baseUrl}/api/products/{categoryId}");
                return result ?? new();
            }
            catch { return new(); }
        }

        // ─── VERESIYE ────────────────────────────────────────
        public async Task<List<PaymentDto>> GetVeresiyeAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<List<PaymentDto>>($"{_baseUrl}/api/veresiye");
                return result ?? new();
            }
            catch { return new(); }
        }
    }

    // ─── DTO'LAR ─────────────────────────────────────────────

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class LoginResponseDto
    {
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public class TableDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Empty";
        public int DisplayOrder { get; set; }
        public decimal CurrentTotal { get; set; }
        public DateTime? LastItemAddedAt { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public int TableId { get; set; }
        public string TableNameSnapshot { get; set; } = "";
        public string Status { get; set; } = "Open";
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastItemAddedAt { get; set; }
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public string NameSnapshot { get; set; } = "";
        public decimal PriceSnapshot { get; set; }
        public string Portion { get; set; } = "Tam";
        public int Quantity { get; set; }
        public int CollectedQuantity { get; set; }
        public int VeresiyeQuantity { get; set; }
    }

    public class PaymentDto
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentType { get; set; } = "Cash";
        public string? CustomerName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AddProductResponse
    {
        public int OrderId { get; set; }
    }

    public class CompletePaymentResponse
    {
        public bool WasFullyPaid { get; set; }
    }
}
