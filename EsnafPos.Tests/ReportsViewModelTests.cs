using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.ViewModels;
using Xunit;

namespace EsnafPos.Tests;

public class ReportsViewModelTests
{
    private static string Today => DateTime.Today.ToString("yyyy-MM-dd");

    private static Order SeedPaidOrder(AppDbContext db, decimal total)
    {
        var order = new Order
        {
            TableId           = 1,
            TableNameSnapshot = "Masa 1",
            Status            = OrderStatus.Paid,
            DayDate           = DateTime.Today.ToString("yyyy-MM-dd"),
            CreatedAt         = DateTime.Now,
            TotalAmount       = total
        };
        db.Orders.Add(order);
        db.SaveChanges();
        return order;
    }

    [Fact]
    public async Task LoadDailyReport_NakitVeKarti_DogruAyirir()
    {
        using var t = new TestDb();
        var order = SeedPaidOrder(t.Db, 100);
        t.Db.Payments.AddRange(
            new Payment { OrderId = order.Id, Amount = 60, PaymentType = PaymentType.Cash,       CreatedAt = DateTime.Now },
            new Payment { OrderId = order.Id, Amount = 40, PaymentType = PaymentType.CardCredit,  CreatedAt = DateTime.Now }
        );
        t.Db.SaveChanges();

        var vm = new ReportsViewModel(t.Db) { SelectedDate = DateTime.Today };
        await vm.LoadDailyReport();

        Assert.Equal(60, vm.DailyCash);
        Assert.Equal(40, vm.DailyCard);
        Assert.Equal(100, vm.DailyTotal);
        Assert.Equal(1, vm.DailyOrderCount);
        Assert.False(vm.DailyHasVeresiye);
    }

    [Fact]
    public async Task LoadDailyReport_Veresiye_AyriGosterilir_NakitKartiEtkilemez()
    {
        using var t = new TestDb();
        var order = SeedPaidOrder(t.Db, 50);
        t.Db.Payments.Add(new Payment
        {
            OrderId = order.Id, Amount = 50, PaymentType = PaymentType.Veresiye,
            CustomerName = "Ahmet", CreatedAt = DateTime.Now
        });
        t.Db.SaveChanges();

        var vm = new ReportsViewModel(t.Db) { SelectedDate = DateTime.Today };
        await vm.LoadDailyReport();

        Assert.Equal(50, vm.DailyVeresiye);
        Assert.True(vm.DailyHasVeresiye);
        Assert.Equal(0, vm.DailyCash);
        Assert.Equal(0, vm.DailyCard);
        Assert.Equal(0, vm.DailyTotal);
    }

    [Fact]
    public async Task LoadProductSales_CollectedVeVeresiyeToplaminiKullanir()
    {
        using var t = new TestDb();
        var order = SeedPaidOrder(t.Db, 0);
        // 5 adetlik kalem: 3 tahsil + 1 veresiye = 4 satılmış say (Quantity=5 değil)
        t.Db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id, ProductId = 7, NameSnapshot = "Çorba", Portion = "Tam",
            PriceSnapshot = 10, Quantity = 5, CollectedQuantity = 3, VeresiyeQuantity = 1
        });
        // Sipariş "geçerli" sayılması için gerçek ödeme kaydı gerekir
        t.Db.Payments.Add(new Payment { OrderId = order.Id, Amount = 30, PaymentType = PaymentType.Cash, CreatedAt = DateTime.Now });
        t.Db.SaveChanges();

        var vm = new ReportsViewModel(t.Db) { SelectedDate = DateTime.Today };
        await vm.LoadDailyReport();

        var sale = Assert.Single(vm.DailyProductSales);
        Assert.Equal("Çorba", sale.ProductName);
        Assert.Equal(4, sale.TotalQuantity);     // 3 + 1
        Assert.Equal(40, sale.TotalRevenue);     // 4 * 10
    }

    [Fact]
    public async Task LoadProductSales_OdemesiOlmayanSiparis_HaricTutulur()
    {
        using var t = new TestDb();
        var order = SeedPaidOrder(t.Db, 0);
        t.Db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id, ProductId = 7, NameSnapshot = "Çay", Portion = "Tam",
            PriceSnapshot = 5, Quantity = 2, CollectedQuantity = 2, VeresiyeQuantity = 0
        });
        // Ödeme kaydı YOK → ürün satışlarına dahil edilmemeli
        t.Db.SaveChanges();

        var vm = new ReportsViewModel(t.Db) { SelectedDate = DateTime.Today };
        await vm.LoadDailyReport();

        Assert.Empty(vm.DailyProductSales);
    }
}
