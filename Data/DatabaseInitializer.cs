using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EsnafPos.Models;
using System.Security.Cryptography;

namespace EsnafPos.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            // Veritabani yoksa olustur
            context.Database.EnsureCreated();

            // Hic kullanici yoksa admin ekle
            if (!context.Users.Any())
            {
                context.Users.AddRange(
                    new User
                    {
                        Username = "admin",
                        PinHash = HashPin("1234"),
                        Role = UserRole.Admin,
                        IsActive = true
                    },
                    new User
                    {
                        Username = "kasiyer",
                        PinHash = HashPin("5678"),
                        Role = UserRole.Cashier,
                        IsActive = true
                    }
                );
                context.SaveChanges();
            }

            // Hic masa yoksa varsayilan masalari ekle
            if (!context.Tables.Any())
            {
                for (int i = 1; i <= 10; i++)
                {
                    context.Tables.Add(new Table
                    {
                        Name = $"Masa {i}",
                        Status = TableStatus.Empty,
                        IsActive = true,
                        DisplayOrder = i
                    });
                }
                context.SaveChanges();
            }

            // Hic kategori yoksa ornek ekle
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Yemekler", IsActive = true, DisplayOrder = 1 },
                    new Category { Name = "Icecekler", IsActive = true, DisplayOrder = 2 },
                    new Category { Name = "Tatlilar", IsActive = true, DisplayOrder = 3 }
                };
                context.Categories.AddRange(categories);
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
