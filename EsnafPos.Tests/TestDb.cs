using EsnafPos.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EsnafPos.Tests;

/// <summary>
/// Her test için izole, bellek-içi (in-memory) bir SQLite AppDbContext üretir.
/// Bağlantı açık tutulduğu sürece veritabanı yaşar; test bitince Dispose edilir.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    public AppDbContext Db { get; }

    public TestDb()
    {
        // FK zorlaması kapalı: testler para/toplama mantığına odaklı, tüm referans
        // grafiğini (Category→Product, Table→Order...) seed'lemeye gerek kalmasın.
        _conn = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conn)
            .Options;
        Db = new AppDbContext(options);
        Db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Db.Dispose();
        _conn.Dispose();
    }
}
