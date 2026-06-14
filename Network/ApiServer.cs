using EsnafPos.Data;
using EsnafPos.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace EsnafPos.Network
{
    /// <summary>
    /// Sunucu modunda çalışan gömülü minimal Web API.
    /// App.xaml.cs içinde arka plan thread'inde başlatılır.
    /// </summary>
    public class ApiServer
    {
        private WebApplication? _app;
        private readonly string _username;
        private readonly string _password;
        private readonly string _dbPath;

        public ApiServer(string username, string password, string dbPath)
        {
            _username = username;
            _password = password;
            _dbPath   = dbPath;
        }

        public async Task StartAsync(int port)
        {
            var builder = WebApplication.CreateBuilder();
            // AppDbContext kendi OnConfiguring'inde DB yolunu buluyor
            builder.Services.AddTransient<AppDbContext>(_ => new AppDbContext());

            // Enum'ları string olarak serialize et (istemci Enum.TryParse ile okur)
            builder.Services.ConfigureHttpJsonOptions(opts =>
            {
                opts.SerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

            _app = builder.Build();

            // ─── AUTH MIDDLEWARE ──────────────────────────────
            _app.Use(async (ctx, next) =>
            {
                // OPTIONS preflight için geç
                if (ctx.Request.Method == "OPTIONS") { await next(); return; }

                if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var key)
                    || key != BuildApiKey(_username, _password))
                {
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("Unauthorized");
                    return;
                }
                await next();
            });

            // ─── LOGIN ───────────────────────────────────────
            _app.MapPost("/api/login", async (
                [FromBody] LoginRequest req,
                AppDbContext db) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u =>
                    u.Username == req.Username &&
                    u.PinHash == req.PinHash &&
                    u.IsActive);
                if (user == null) return Results.Unauthorized();
                return Results.Ok(new { user.Username, Role = user.Role.ToString() });
            });

            // Kullanici listesi (istemci login ekrani icin)
            _app.MapGet("/api/users", async (AppDbContext db) =>
            {
                var users = await db.Users
                    .Where(u => u.IsActive)
                    .OrderBy(u => u.Role)
                    .ThenBy(u => u.Username)
                    .Select(u => new { u.Id, u.Username, Role = u.Role.ToString() })
                    .ToListAsync();
                return Results.Ok(users);
            });

            // ─── TABLES ──────────────────────────────────────
            _app.MapGet("/api/tables", async (AppDbContext db) =>
            {
                var tables = await db.Tables
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.DisplayOrder)
                    .ToListAsync();

                var activeOrders = await db.Orders
                    .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Veresiye)
                    .Include(o => o.Items)
                    .ToListAsync();

                var result = tables.Select(t =>
                {
                    var order = activeOrders.FirstOrDefault(o =>
                        o.TableId == t.Id && o.Status == OrderStatus.Open);
                    decimal total = activeOrders
                        .Where(o => o.TableId == t.Id)
                        .Sum(o => o.Items.Sum(i =>
                            (i.Quantity - i.CollectedQuantity - i.VeresiyeQuantity) * i.PriceSnapshot));
                    return new
                    {
                        t.Id, t.Name, t.Status, t.DisplayOrder,
                        CurrentTotal = total,
                        LastItemAddedAt = order?.LastItemAddedAt
                    };
                });
                return Results.Ok(result);
            });

            // ─── ORDERS ──────────────────────────────────────
            _app.MapGet("/api/orders/open", async (AppDbContext db) =>
            {
                var orders = await db.Orders
                    .Where(o => o.Status == OrderStatus.Open || o.Status == OrderStatus.Veresiye)
                    .Include(o => o.Items)
                    .ToListAsync();
                // DTO'ya projekte et — ham entity'de Order<->OrderItem geri-referansi
                // System.Text.Json'da "object cycle" hatasi verip 500'e yol aciyor.
                return Results.Ok(orders.Select(o => new {
                    o.Id, o.TableId, o.TableNameSnapshot,
                    Status = o.Status.ToString(),
                    o.TotalAmount, o.CreatedAt, o.LastItemAddedAt,
                    Items = o.Items.Select(i => new {
                        i.Id, i.OrderId, i.ProductId,
                        i.NameSnapshot, i.PriceSnapshot,
                        i.Portion, i.Quantity,
                        i.CollectedQuantity, i.VeresiyeQuantity
                    })
                }));
            });

            _app.MapGet("/api/orders/{tableId:int}", async (int tableId, AppDbContext db) =>
            {
                var order = await db.Orders
                    .Where(o => o.TableId == tableId && o.Status == OrderStatus.Open)
                    .FirstOrDefaultAsync();
                if (order is null) return Results.NotFound();
                return Results.Ok(new {
                    order.Id, order.TableId, order.TableNameSnapshot,
                    Status = order.Status.ToString(),
                    order.TotalAmount, order.CreatedAt, order.LastItemAddedAt
                });
            });

            // ─── ORDER ITEMS ─────────────────────────────────
            _app.MapGet("/api/orderitems/{orderId:int}", async (int orderId, AppDbContext db) =>
            {
                var items = await db.OrderItems
                    .Where(i => i.OrderId == orderId)
                    .ToListAsync();
                return Results.Ok(items.Select(i => new {
                    i.Id, i.OrderId, i.ProductId,
                    i.NameSnapshot, i.PriceSnapshot,
                    i.Portion, i.Quantity,
                    i.CollectedQuantity, i.VeresiyeQuantity
                }));
            });

            // ─── ADD PRODUCT ─────────────────────────────────
            _app.MapPost("/api/orders/{tableId:int}/addproduct", async (
                int tableId,
                [FromBody] AddProductRequest req,
                AppDbContext db) =>
            {
                var table = await db.Tables.FindAsync(tableId);
                if (table == null) return Results.NotFound("Masa bulunamadı");

                var order = await db.Orders.FirstOrDefaultAsync(o =>
                    o.TableId == tableId && o.Status == OrderStatus.Open);

                if (order == null)
                {
                    order = new Order
                    {
                        TableId           = tableId,
                        TableNameSnapshot = table.Name,
                        Status            = OrderStatus.Open,
                        DayDate           = DateTime.Today.ToString("yyyy-MM-dd"),
                        CreatedAt         = DateTime.Now
                    };
                    db.Orders.Add(order);
                    table.Status = TableStatus.Active;
                    db.Tables.Update(table);
                    await db.SaveChangesAsync();
                }

                var existing = await db.OrderItems.FirstOrDefaultAsync(i =>
                    i.OrderId == order.Id &&
                    i.ProductId == req.ProductId &&
                    i.Portion == req.Portion);

                if (existing != null)
                {
                    existing.Quantity++;
                    db.OrderItems.Update(existing);
                }
                else
                {
                    db.OrderItems.Add(new OrderItem
                    {
                        OrderId       = order.Id,
                        ProductId     = req.ProductId,
                        NameSnapshot  = req.NameSnapshot,
                        PriceSnapshot = req.PriceSnapshot,
                        Portion       = req.Portion,
                        Quantity      = 1
                    });
                }

                order.LastItemAddedAt = DateTime.Now;
                order.TotalAmount = (await db.OrderItems
                    .Where(i => i.OrderId == order.Id)
                    .ToListAsync())
                    .Sum(i => i.Quantity * i.PriceSnapshot);
                db.Orders.Update(order);
                await db.SaveChangesAsync();

                return Results.Ok(new { orderId = order.Id });
            });

            // ─── PAYMENTS ────────────────────────────────────
            _app.MapGet("/api/payments/{orderId:int}", async (int orderId, AppDbContext db) =>
            {
                var payments = await db.Payments
                    .Where(p => p.OrderId == orderId)
                    .ToListAsync();
                return Results.Ok(payments.Select(p => new {
                    p.Id, p.OrderId, p.Amount,
                    PaymentType = p.PaymentType.ToString(),
                    p.CustomerName, p.CreatedAt
                }));
            });

            _app.MapPost("/api/payments", async (
                [FromBody] CompletePaymentRequest req,
                AppDbContext db) =>
            {
                var order = await db.Orders.FindAsync(req.OrderId);
                var table = order != null ? await db.Tables.FindAsync(order.TableId) : null;
                if (order == null || table == null) return Results.NotFound();

                foreach (var p in req.Payments)
                {
                    db.Payments.Add(new Payment
                    {
                        OrderId      = req.OrderId,
                        Amount       = p.Amount,
                        PaymentType  = p.PaymentType,
                        CustomerName = p.CustomerName,
                        CreatedAt    = DateTime.Now
                    });
                }

                foreach (var c in req.CashConsumed)
                {
                    var item = await db.OrderItems.FindAsync(c.OrderItemId);
                    if (item != null) { item.CollectedQuantity += c.Quantity; db.OrderItems.Update(item); }
                }
                foreach (var v in req.VeresiyeConsumed)
                {
                    var item = await db.OrderItems.FindAsync(v.OrderItemId);
                    if (item != null) { item.VeresiyeQuantity += v.Quantity; db.OrderItems.Update(item); }
                }

                var allItems = await db.OrderItems.Where(i => i.OrderId == req.OrderId).ToListAsync();
                bool allPaid = allItems.All(i => i.CollectedQuantity + i.VeresiyeQuantity >= i.Quantity);
                bool hasVer  = req.Payments.Any(p => p.PaymentType == PaymentType.Veresiye);

                order.TotalAmount = allItems.Sum(i => i.Quantity * i.PriceSnapshot);
                order.PaymentType = req.Payments
                    .GroupBy(p => p.PaymentType)
                    .OrderByDescending(g => g.Sum(p => p.Amount))
                    .First().Key;

                if (allPaid)
                {
                    order.Status   = hasVer ? OrderStatus.Veresiye : OrderStatus.Paid;
                    order.ClosedAt = DateTime.Now;
                    table.Status   = TableStatus.Empty;
                }
                else
                {
                    order.Status           = OrderStatus.Open;
                    table.Status           = TableStatus.Active;
                    order.LastItemAddedAt  = DateTime.Now;
                }

                db.Orders.Update(order);
                db.Tables.Update(table);
                await db.SaveChangesAsync();

                return Results.Ok(new { wasFullyPaid = allPaid });
            });

            // ─── CATEGORIES + PRODUCTS ────────────────────────
            _app.MapGet("/api/categories", async (AppDbContext db) =>
            {
                var cats = await db.Categories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();
                return Results.Ok(cats.Select(cat => new {
                    cat.Id, cat.Name,
                    cat.DisplayOrder, cat.IsActive,
                    Channel = string.IsNullOrWhiteSpace(cat.Channel) ? "Masa" : cat.Channel
                }));
            });

            _app.MapGet("/api/products/{categoryId:int}", async (int categoryId, AppDbContext db) =>
            {
                var prods = await db.Products
                    .Where(p => p.CategoryId == categoryId && p.IsActive)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();
                return Results.Ok(prods.Select(p => new {
                    p.Id, p.Name, p.CategoryId,
                    p.PriceTam, p.PriceAz, p.PriceBucuk,
                    p.DisplayOrder, p.IsActive
                }));
            });

            // ─── VERESIYE ────────────────────────────────────
            _app.MapGet("/api/veresiye", async (AppDbContext db) =>
            {
                var payments = await db.Payments
                    .Where(p => p.PaymentType == PaymentType.Veresiye && p.CustomerName != null)
                    .ToListAsync();
                return Results.Ok(payments.Select(p => new {
                    p.Id, p.OrderId, p.Amount,
                    PaymentType = p.PaymentType.ToString(),
                    p.CustomerName, p.CreatedAt
                }));
            });

            // ─── REMOVE ITEM ─────────────────────────────────
            _app.MapPost("/api/orderitems/{itemId:int}/remove", async (int itemId, AppDbContext db) =>
            {
                var item = await db.OrderItems.FindAsync(itemId);
                if (item == null) return Results.NotFound();

                // *** KURAL: OrderItem asla fiziksel olarak silinmez (rapor butunlugu) ***
                // Quantity 1'e inince 0'a cekilir, kayit DB'de kalir
                if (item.Quantity > 1)
                    item.Quantity--;
                else
                    item.Quantity = 0;

                db.OrderItems.Update(item);
                await db.SaveChangesAsync();

                // Siparisin kalan urunleri bitti mi kontrol et
                // Kalan = Quantity - CollectedQuantity - VeresiyeQuantity
                var remaining = await db.OrderItems
                    .Where(i => i.OrderId == item.OrderId)
                    .ToListAsync();

                bool allGone = remaining.All(i =>
                    i.Quantity <= i.CollectedQuantity + i.VeresiyeQuantity);

                if (allGone)
                {
                    var order = await db.Orders.FindAsync(item.OrderId);
                    if (order != null && order.Status == OrderStatus.Open)
                    {
                        var table = await db.Tables.FindAsync(order.TableId);
                        // *** KURAL: Order da silinmez, Cancelled yapilir ***
                        order.Status    = OrderStatus.Cancelled;
                        order.TotalAmount = 0;
                        order.ClosedAt  = DateTime.Now;
                        db.Orders.Update(order);
                        if (table != null) { table.Status = TableStatus.Empty; db.Tables.Update(table); }
                        await db.SaveChangesAsync();
                    }
                }
                return Results.Ok();
            });

            // ─── ADD QUANTITY ─────────────────────────────────
            _app.MapPost("/api/orderitems/{itemId:int}/addquantity", async (int itemId, AppDbContext db) =>
            {
                var item = await db.OrderItems.FindAsync(itemId);
                if (item == null) return Results.NotFound();
                item.Quantity++;
                db.OrderItems.Update(item);
                await db.SaveChangesAsync();
                return Results.Ok();
            });

            await _app.RunAsync($"http://0.0.0.0:{port}");
        }

        public async Task StopAsync()
        {
            if (_app != null) await _app.StopAsync();
        }

        public static string BuildApiKey(string username, string password)
        {
            var raw = $"{username}:{password}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }
    }

    // ─── REQUEST MODELLER ────────────────────────────────────

    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string PinHash { get; set; } = "";
    }

    public class AddProductRequest
    {
        public int ProductId { get; set; }
        public string NameSnapshot { get; set; } = "";
        public decimal PriceSnapshot { get; set; }
        public string Portion { get; set; } = "Tam";
    }

    public class PaymentEntryDto
    {
        public PaymentType PaymentType { get; set; }
        public decimal Amount { get; set; }
        public string? CustomerName { get; set; }
    }

    public class ConsumedItemDto
    {
        public int OrderItemId { get; set; }
        public int Quantity { get; set; }
    }

    public class CompletePaymentRequest
    {
        public int OrderId { get; set; }
        public List<PaymentEntryDto> Payments { get; set; } = new();
        public List<ConsumedItemDto> CashConsumed { get; set; } = new();
        public List<ConsumedItemDto> VeresiyeConsumed { get; set; } = new();
    }
}
