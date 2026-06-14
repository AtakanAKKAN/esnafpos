using EsnafPos.Models;
using System.Security.Cryptography;
using System.Text;

namespace EsnafPos.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // EnsureCreated eski DB'ye yeni tabloları eklemez —
            // AppChannels'ı seed'den önce garantiye al (aksi halde AppChannels.Any() patlar)
            context.EnsureChannelsTable();

            // Kullanıcı yoksa admin + kasiyer ekle
            if (!context.Users.Any())
            {
                context.Users.AddRange(
                    new User { Username = "admin",   PinHash = HashPin("1234"), Role = UserRole.Admin,   IsActive = true },
                    new User { Username = "kasiyer", PinHash = HashPin("5678"), Role = UserRole.Cashier, IsActive = true }
                );
                context.SaveChanges();
            }

            // Masa yoksa varsayılan masalar
            if (!context.Tables.Any())
            {
                for (int i = 1; i <= 10; i++)
                    context.Tables.Add(new Table { Name = $"Masa {i}", Status = TableStatus.Empty, IsActive = true, DisplayOrder = i });
                context.SaveChanges();
            }

            // Kategori yoksa örnekler
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Yemekler",  IsActive = true, DisplayOrder = 1 },
                    new Category { Name = "Icecekler", IsActive = true, DisplayOrder = 2 },
                    new Category { Name = "Tatlilar",  IsActive = true, DisplayOrder = 3 }
                );
                context.SaveChanges();
            }

            // Kanal yoksa varsayılan kanallar
            if (!context.AppChannels.Any())
            {
                var defaults = new[] { "Masa", "Kurye", "Bekci", "Trendyol", "Diger" };
                for (int i = 0; i < defaults.Length; i++)
                    context.AppChannels.Add(new AppChannel { Name = defaults[i], DisplayOrder = i + 1, IsActive = true });
                context.SaveChanges();
            }
        }

        public static string HashPin(string pin)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
