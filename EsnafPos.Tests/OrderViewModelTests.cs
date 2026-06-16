using EsnafPos.Data;
using EsnafPos.Models;
using EsnafPos.Services;
using EsnafPos.ViewModels;
using Xunit;

namespace EsnafPos.Tests;

public class OrderViewModelTests
{
    private static OrderViewModel NewVm(AppDbContext db)
        => new OrderViewModel(db, new PrinterService(new SettingsService()), new SessionService());

    private static Table NewTable(AppDbContext db, string name, TableStatus status)
    {
        var t = new Table { Name = name, Status = status, IsActive = true, DisplayOrder = 1 };
        db.Tables.Add(t);
        db.SaveChanges();
        return t;
    }

    private static Order NewOrder(AppDbContext db, Table table, OrderStatus status = OrderStatus.Open)
    {
        var o = new Order
        {
            TableId = table.Id, TableNameSnapshot = table.Name, Status = status,
            DayDate = DateTime.Today.ToString("yyyy-MM-dd"), CreatedAt = DateTime.Now
        };
        db.Orders.Add(o);
        db.SaveChanges();
        return o;
    }

    private static void AddItem(AppDbContext db, Order order, int productId, string name, string portion, int qty, decimal price)
    {
        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.Id, ProductId = productId, NameSnapshot = name, PriceSnapshot = price,
            Portion = portion, Quantity = qty, CollectedQuantity = 0, VeresiyeQuantity = 0
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task MoveToTable_BosMasayaTasir_KaynakBosalir_HedefAktif()
    {
        using var t = new TestDb();
        var source = NewTable(t.Db, "Masa 1", TableStatus.Active);
        var target = NewTable(t.Db, "Masa 2", TableStatus.Empty);
        var order = NewOrder(t.Db, source);

        var vm = NewVm(t.Db);
        vm.CurrentOrder = order;
        vm.CurrentTable = source;

        var err = await vm.MoveToTable(target);

        Assert.Null(err);
        Assert.Equal(target.Id, order.TableId);
        Assert.Equal(TableStatus.Empty, source.Status);
        Assert.Equal(TableStatus.Active, target.Status);
    }

    [Fact]
    public async Task MoveToTable_AktifSiparisYok_HataDoner()
    {
        using var t = new TestDb();
        var target = NewTable(t.Db, "Masa 2", TableStatus.Empty);
        var vm = NewVm(t.Db); // CurrentOrder null

        var err = await vm.MoveToTable(target);

        Assert.NotNull(err);
    }

    [Fact]
    public async Task MoveToVeresiye_KalanlarVeresiyeYazilir_StatusVeresiye_MasaBosalir()
    {
        using var t = new TestDb();
        var table = NewTable(t.Db, "Masa 1", TableStatus.Active);
        var order = NewOrder(t.Db, table);
        AddItem(t.Db, order, productId: 1, "Çorba", "Tam", qty: 2, price: 10);
        AddItem(t.Db, order, productId: 2, "Çay",   "Tam", qty: 1, price: 5);

        var vm = NewVm(t.Db);
        vm.CurrentOrder = order;
        vm.CurrentTable = table;

        var err = await vm.MoveToVeresiye("Mehmet");

        Assert.Null(err);
        var items = t.Db.OrderItems.Where(i => i.OrderId == order.Id).ToList();
        Assert.All(items, i => Assert.Equal(i.Quantity, i.VeresiyeQuantity)); // tamamı veresiye
        var pays = t.Db.Payments.Where(p => p.OrderId == order.Id).ToList();
        Assert.Equal(2, pays.Count);                                    // her kalem için bir veresiye kaydı
        Assert.All(pays, p => Assert.Equal(PaymentType.Veresiye, p.PaymentType));
        Assert.All(pays, p => Assert.Equal("Mehmet", p.CustomerName));
        Assert.Equal(25, pays.Sum(p => p.Amount));                      // 2*10 + 1*5
        Assert.Equal(OrderStatus.Veresiye, order.Status);
        Assert.Equal(TableStatus.Empty, table.Status);
    }

    [Fact]
    public async Task MergeFromTable_UrunlerHedefeTasinir_AyniUrunToplanir_KaynakIptal()
    {
        using var t = new TestDb();
        var targetTable = NewTable(t.Db, "Masa 1", TableStatus.Active);
        var sourceTable = NewTable(t.Db, "Masa 2", TableStatus.Active);

        var targetOrder = NewOrder(t.Db, targetTable);
        AddItem(t.Db, targetOrder, productId: 1, "Çorba", "Tam", qty: 1, price: 10);

        var sourceOrder = NewOrder(t.Db, sourceTable);
        AddItem(t.Db, sourceOrder, productId: 1, "Çorba", "Tam", qty: 2, price: 10); // aynı ürün+porsiyon → toplanmalı
        AddItem(t.Db, sourceOrder, productId: 2, "Çay",   "Tam", qty: 1, price: 5);  // yeni kalem

        var vm = NewVm(t.Db);
        vm.CurrentOrder = targetOrder;
        vm.CurrentTable = targetTable;

        var err = await vm.MergeFromTable(sourceTable);

        Assert.Null(err);

        var targetItems = t.Db.OrderItems.Where(i => i.OrderId == targetOrder.Id).ToList();
        var corba = targetItems.Single(i => i.NameSnapshot == "Çorba" && i.Portion == "Tam");
        Assert.Equal(3, corba.Quantity);                       // 1 + 2 toplandı
        Assert.Contains(targetItems, i => i.NameSnapshot == "Çay"); // yeni kalem eklendi

        // Kaynak sipariş iptal, kaynak item'lar FİZİKSEL SİLİNMEZ (collected olarak işaretlenir)
        Assert.Equal(OrderStatus.Cancelled, sourceOrder.Status);
        var sourceItems = t.Db.OrderItems.Where(i => i.OrderId == sourceOrder.Id).ToList();
        Assert.NotEmpty(sourceItems);
        Assert.All(sourceItems, i => Assert.Equal(i.Quantity, i.CollectedQuantity));
        Assert.Equal(TableStatus.Empty, sourceTable.Status);
    }
}
