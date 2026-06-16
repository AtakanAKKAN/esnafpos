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
        private readonly List<string> _candidates;
        private string _baseUrl;
        private bool _isConnected = false;
        private bool _resolved    = false;

        // Son başarılı sunucu IP'si — isim çözümlemesi tamamen çökerse son çare
        private static readonly string _ipCachePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "EsnafPos", "server_ip.cache");

        public bool   IsConnected => _isConnected;
        public string BaseUrl     => _baseUrl;

        // serverHost: IP veya bilgisayar adı (hostname). Ad yazılırsa IP değişse bile
        // OS adı her seferinde tazeden çözer; ayrıca .local (mDNS) ve önbellekteki IP
        // yedek aday olarak denenir.
        public ApiClient(string serverHost, int port, string username, string password)
        {
            var host    = (serverHost ?? "").Trim();
            _candidates = BuildCandidates(host, port);
            _baseUrl    = _candidates.Count > 0 ? _candidates[0] : $"http://{host}:{port}";
            _http       = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            _http.DefaultRequestHeaders.Add("X-Api-Key",
                ApiServer.BuildApiKey(username, password));
        }

        private static List<string> BuildCandidates(string host, int port)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(host))
            {
                list.Add($"http://{host}:{port}");
                // Hostname (IP değil ve nokta içermiyor) ise mDNS .local varyantını da dene
                bool isIp = System.Net.IPAddress.TryParse(host, out _);
                if (!isIp && !host.Contains('.'))
                    list.Add($"http://{host}.local:{port}");
            }
            try
            {
                if (System.IO.File.Exists(_ipCachePath))
                {
                    var cachedIp  = System.IO.File.ReadAllText(_ipCachePath).Trim();
                    var cachedUrl = $"http://{cachedIp}:{port}";
                    if (!string.IsNullOrWhiteSpace(cachedIp) && !list.Contains(cachedUrl))
                        list.Add(cachedUrl);
                }
            }
            catch { }
            return list;
        }

        // İlk çalışan adayı bulup _baseUrl yapar; bulunan adresi IP olarak önbelleğe yazar.
        private async Task EnsureResolvedAsync()
        {
            if (_resolved) return;
            foreach (var candidate in _candidates)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var resp = await _http.GetAsync($"{candidate}/api/tables", cts.Token);
                    if (resp.IsSuccessStatusCode)
                    {
                        _baseUrl     = candidate;
                        _resolved    = true;
                        _isConnected = true;
                        await CacheResolvedIpAsync(candidate);
                        return;
                    }
                }
                catch { /* sonraki adayı dene */ }
            }
            _isConnected = false;
        }

        private static async Task CacheResolvedIpAsync(string baseUrl)
        {
            try
            {
                var host = new Uri(baseUrl).Host;
                string ip;
                if (System.Net.IPAddress.TryParse(host, out var parsed))
                    ip = parsed.ToString();
                else
                {
                    var addrs = await System.Net.Dns.GetHostAddressesAsync(host);
                    var v4 = addrs.FirstOrDefault(a =>
                        a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (v4 == null) return;
                    ip = v4.ToString();
                }
                await System.IO.File.WriteAllTextAsync(_ipCachePath, ip);
            }
            catch { }
        }

        // ─── DAYANIKLI GET ───────────────────────────────────
        // İdempotent okuma: gerekirse adresi çöz, gönder. Yalnız BAĞLANTI koparsa
        // (exception) adres bayatlamış olabilir → yeniden çöz ve bir kez tekrar dene.
        // 4xx/5xx (sunucu ulaşılabilir, içerik yok) yeniden-çözümlemeyi tetiklemez.
        private async Task<T?> GetJsonAsync<T>(string path)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    await EnsureResolvedAsync();
                    var resp = await _http.GetAsync($"{_baseUrl}{path}");
                    _isConnected = resp.IsSuccessStatusCode;
                    return resp.IsSuccessStatusCode
                        ? await resp.Content.ReadFromJsonAsync<T>()
                        : default;
                }
                catch
                {
                    _isConnected = false;
                    _resolved    = false;   // bağlantı koptu → sonraki denemede yeniden çöz
                }
            }
            return default;
        }

        // ─── BAĞLANTI TEST ───────────────────────────────────
        public async Task<bool> TestConnectionAsync()
        {
            // Her testte baştan dene — sunucu adresi değişmiş olabilir, hostname'i tercih et
            _resolved = false;
            await EnsureResolvedAsync();
            return _isConnected;
        }

        // ─── LOGIN ───────────────────────────────────────────
        public async Task<LoginResponseDto?> LoginAsync(string username, string pinHash)
        {
            try
            {
                await EnsureResolvedAsync();
                var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/login",
                    new { Username = username, PinHash = pinHash });
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<LoginResponseDto>();
            }
            catch { _resolved = false; return null; }
        }

        public async Task<List<UserDto>> GetUsersAsync()
            => await GetJsonAsync<List<UserDto>>("/api/users") ?? new();

        // ─── TABLES ──────────────────────────────────────────
        public async Task<List<TableDto>> GetTablesAsync()
            => await GetJsonAsync<List<TableDto>>("/api/tables") ?? new();

        // ─── ORDERS ──────────────────────────────────────────
        public async Task<OrderDto?> GetOrderForTableAsync(int tableId)
            => await GetJsonAsync<OrderDto>($"/api/orders/{tableId}");

        public async Task<List<OrderItemDto>> GetOrderItemsAsync(int orderId)
            => await GetJsonAsync<List<OrderItemDto>>($"/api/orderitems/{orderId}") ?? new();

        // ─── ADD PRODUCT ─────────────────────────────────────
        public async Task<int?> AddProductAsync(int tableId, AddProductRequest req)
        {
            try
            {
                await EnsureResolvedAsync();
                var resp = await _http.PostAsJsonAsync(
                    $"{_baseUrl}/api/orders/{tableId}/addproduct", req);
                if (!resp.IsSuccessStatusCode) return null;
                var result = await resp.Content.ReadFromJsonAsync<AddProductResponse>();
                return result?.OrderId;
            }
            catch { _resolved = false; return null; }
        }

        // ─── ADD QUANTITY / REMOVE ────────────────────────────
        public async Task<bool> AddQuantityAsync(int itemId)
        {
            try
            {
                await EnsureResolvedAsync();
                var resp = await _http.PostAsync(
                    $"{_baseUrl}/api/orderitems/{itemId}/addquantity", null);
                return resp.IsSuccessStatusCode;
            }
            catch { _resolved = false; return false; }
        }

        public async Task<bool> RemoveItemAsync(int itemId)
        {
            try
            {
                await EnsureResolvedAsync();
                var resp = await _http.PostAsync(
                    $"{_baseUrl}/api/orderitems/{itemId}/remove", null);
                return resp.IsSuccessStatusCode;
            }
            catch { _resolved = false; return false; }
        }

        // ─── PAYMENTS ────────────────────────────────────────
        public async Task<List<PaymentDto>> GetPaymentsAsync(int orderId)
            => await GetJsonAsync<List<PaymentDto>>($"/api/payments/{orderId}") ?? new();

        public async Task<CompletePaymentResponse?> CompletePaymentAsync(CompletePaymentRequest req)
        {
            try
            {
                await EnsureResolvedAsync();
                var resp = await _http.PostAsJsonAsync($"{_baseUrl}/api/payments", req);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<CompletePaymentResponse>();
            }
            catch { _resolved = false; return null; }
        }

        // ─── CATEGORIES + PRODUCTS ────────────────────────────
        public async Task<List<Category>> GetCategoriesAsync()
            => await GetJsonAsync<List<Category>>("/api/categories") ?? new();

        public async Task<List<Product>> GetProductsAsync(int categoryId)
            => await GetJsonAsync<List<Product>>($"/api/products/{categoryId}") ?? new();

        // ─── VERESIYE ────────────────────────────────────────
        public async Task<List<PaymentDto>> GetVeresiyeAsync()
            => await GetJsonAsync<List<PaymentDto>>("/api/veresiye") ?? new();
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
