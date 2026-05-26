using EsnafPos.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace EsnafPos.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Table>          Tables          { get; set; }
        public DbSet<Category>       Categories      { get; set; }
        public DbSet<Product>        Products        { get; set; }
        public DbSet<Order>          Orders          { get; set; }
        public DbSet<OrderItem>      OrderItems      { get; set; }
        public DbSet<User>           Users           { get; set; }
        public DbSet<Refund>         Refunds         { get; set; }
        public DbSet<Payment>        Payments        { get; set; }
        public DbSet<OrderChangeLog> OrderChangeLogs { get; set; }
        public DbSet<AppChannel>     AppChannels     { get; set; }  // Kanal yonetimi

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EsnafPos", "esnafpos.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            options.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .Property(p => p.Price).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.PriceSnapshot).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderItem>()
                .Ignore(o => o.LineTotal);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount).HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.PaymentType).HasConversion<string>();

            modelBuilder.Entity<Payment>()
                .Property(p => p.CustomerName).IsRequired(false);

            modelBuilder.Entity<Table>()
                .Property(t => t.Status).HasConversion<string>();

            modelBuilder.Entity<Order>()
                .Property(o => o.Status).HasConversion<string>();

            modelBuilder.Entity<Order>()
                .Property(o => o.PaymentType).HasConversion<string>();

            modelBuilder.Entity<User>()
                .Property(u => u.Role).HasConversion<string>();
        }

        // ─── Ensure metodları (eski kurulumlar için) ─────────────

        public void EnsureChannelsTable()
        {
            try
            {
                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS AppChannels (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name         TEXT    NOT NULL DEFAULT '',
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    IsActive     INTEGER NOT NULL DEFAULT 1
                )");
            }
            catch { }
        }

        public void EnsureCategoryChannelColumn()
        {
            try
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Categories ADD COLUMN Channel TEXT NOT NULL DEFAULT 'Masa'");
            }
            catch { }
        }

        public void EnsureOrderChangeLogTable()
        {
            try
            {
                Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS OrderChangeLogs (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId         INTEGER NOT NULL,
                    TableName       TEXT    NOT NULL DEFAULT '',
                    ProductName     TEXT    NOT NULL DEFAULT '',
                    Portion         TEXT    NOT NULL DEFAULT '',
                    QuantityRemoved INTEGER NOT NULL DEFAULT 1,
                    UnitPrice       DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Reason          TEXT    NOT NULL DEFAULT '',
                    CashierName     TEXT    NOT NULL DEFAULT '',
                    DayDate         TEXT    NOT NULL DEFAULT '',
                    CreatedAt       TEXT    NOT NULL DEFAULT ''
                )");
            }
            catch { }
        }

        public void EnsureCollectedQuantityColumn()
        {
            try
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE OrderItems ADD COLUMN CollectedQuantity INTEGER NOT NULL DEFAULT 0");
            }
            catch { }
        }

        public void EnsureCustomerNameColumn()
        {
            try
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Payments ADD COLUMN CustomerName TEXT NULL");
            }
            catch { }
        }

        public void EnsureLastItemAddedAtColumn()
        {
            try
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE Orders ADD COLUMN LastItemAddedAt TEXT NULL");
            }
            catch { }
        }

        public void EnsureVeresiyeQuantityColumn()
        {
            try
            {
                Database.ExecuteSqlRaw(
                    "ALTER TABLE OrderItems ADD COLUMN VeresiyeQuantity INTEGER NOT NULL DEFAULT 0");
            }
            catch { }
        }
    }
}
