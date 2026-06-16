using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using EsnafPos.ViewModels;
using EsnafPos.Views;
using Xunit;

namespace EsnafPos.Tests;

public class PaymentViewModelTests
{
    private static PaymentViewModel NewVm(AppDbContext db)
        => new PaymentViewModel(db, new PrinterService(new SettingsService()));

    private static (Order order, Table table, OrderItem item) Seed(AppDbContext db, int qty, decimal price)
    {
        var table = new Table { Name = "Masa 1", Status = TableStatus.Active, IsActive = true, DisplayOrder = 1 };
        db.Tables.Add(table);
        db.SaveChanges();

        var order = new Order
        {
            TableId = table.Id, TableNameSnapshot = table.Name, Status = OrderStatus.Open,
            DayDate = DateTime.Today.ToString("yyyy-MM-dd"), CreatedAt = DateTime.Now
        };
        db.Orders.Add(order);
        db.SaveChanges();

        var item = new OrderItem
        {
            OrderId = order.Id, ProductId = 1, NameSnapshot = "Çorba", PriceSnapshot = price,
            Portion = "Tam", Quantity = qty, CollectedQuantity = 0, VeresiyeQuantity = 0
        };
        db.OrderItems.Add(item);
        db.SaveChanges();
        return (order, table, item);
    }

    private static PaymentEntry CashEntry(decimal amount, int itemId, int qty) => new()
    {
        PaymentType = PaymentType.Cash,
        Amount = amount,
        ConsumedItems = new() { new ConsumedItemSnapshot { OrderItemId = itemId, Quantity = qty } }
    };

    [Fact]
    public async Task KismiNakit_CollectedQuantityArtar_SiparisAcikKalir()
    {
        using var t = new TestDb();
        var (order, table, item) = Seed(t.Db, qty: 2, price: 50);

        var vm = NewVm(t.Db);
        await vm.Load(order, table);
        await vm.CompletePaymentWithEntries(new() { CashEntry(50, item.Id, 1) }, skipPrint: true);

        var dbItem = t.Db.OrderItems.Find(item.Id)!;
        Assert.Equal(1, dbItem.CollectedQuantity);   // 2 adetten 1'i tahsil
        Assert.Equal(0, dbItem.VeresiyeQuantity);
        Assert.Equal(OrderStatus.Open, order.Status); // hepsi ödenmedi
        Assert.Equal(TableStatus.Active, table.Status);
        Assert.False(vm.WasFullyPaid);
        Assert.Single(t.Db.Payments.Where(p => p.OrderId == order.Id));
    }

    [Fact]
    public async Task TamNakit_HepsiOdenir_StatusPaid_MasaBosalir()
    {
        using var t = new TestDb();
        var (order, table, item) = Seed(t.Db, qty: 1, price: 50);

        var vm = NewVm(t.Db);
        await vm.Load(order, table);
        await vm.CompletePaymentWithEntries(new() { CashEntry(50, item.Id, 1) }, skipPrint: true);

        var dbItem = t.Db.OrderItems.Find(item.Id)!;
        Assert.Equal(1, dbItem.CollectedQuantity);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(TableStatus.Empty, table.Status);
        Assert.True(vm.WasFullyPaid);
    }

    [Fact]
    public async Task Veresiye_VeresiyeQuantityArtar_StatusVeresiye()
    {
        using var t = new TestDb();
        var (order, table, item) = Seed(t.Db, qty: 1, price: 50);

        var entry = new PaymentEntry
        {
            PaymentType = PaymentType.Veresiye,
            Amount = 50,
            CustomerName = "Ali",
            ConsumedItems = new() { new ConsumedItemSnapshot { OrderItemId = item.Id, Quantity = 1 } }
        };

        var vm = NewVm(t.Db);
        await vm.Load(order, table);
        await vm.CompletePaymentWithEntries(new() { entry }, skipPrint: true);

        var dbItem = t.Db.OrderItems.Find(item.Id)!;
        Assert.Equal(1, dbItem.VeresiyeQuantity);
        Assert.Equal(0, dbItem.CollectedQuantity);
        Assert.Equal(OrderStatus.Veresiye, order.Status);
        Assert.Equal(TableStatus.Empty, table.Status);
        var pay = Assert.Single(t.Db.Payments.Where(p => p.OrderId == order.Id));
        Assert.Equal(PaymentType.Veresiye, pay.PaymentType);
        Assert.Equal("Ali", pay.CustomerName);
    }
}
