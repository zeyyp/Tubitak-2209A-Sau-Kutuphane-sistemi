using ReservationService.Data;

namespace ReservationService.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ReservationDbContext context)
        {
            SeedFaculties(context);
            SeedTables(context);
        }

        private static void SeedFaculties(ReservationDbContext context)
        {
            var facultyNames = new[]
            {
                "Fen Fakültesi",
                "Mühendislik Fakültesi",
                "Tıp Fakültesi",
                "Bilgisayar ve Bilişim Bilimleri Fakültesi",
                "Sağlık Bilimleri Fakültesi",
                "Diş Hekimliği Fakültesi",
                "Hukuk Fakültesi",
                "Eğitim Fakültesi",
                "İnsan ve Toplum Bilimleri Fakültesi",
                "İşletme Fakültesi",
                "İlahiyat Fakültesi",
                "İletişim Fakültesi",
                "Sanat Tasarım ve Mimarlık Fakültesi",
                "Siyasal Bilgiler Fakültesi",
                "Teknik Eğitim Fakültesi"
            };

            var existingFaculties = context.Faculties.Select(f => f.Name).ToHashSet();
            var facultiesToAdd = facultyNames.Where(n => !existingFaculties.Contains(n))
                                             .Select(n => new Faculty { Name = n })
                                             .ToList();

            if (facultiesToAdd.Any())
            {
                context.Faculties.AddRange(facultiesToAdd);
                context.SaveChanges();
            }
        }

        private static void SeedTables(ReservationDbContext context)
        {
            if (context.Tables.Any())
            {
                return;   // DB has been seeded with tables
            }

            var tables = new List<Table>();

            // Floor 1
            for (int i = 1; i <= 10; i++)
            {
                tables.Add(new Table { TableNumber = $"Masa 1-{i}", FloorId = 1 });
            }

            // Floor 2
            for (int i = 1; i <= 10; i++)
            {
                tables.Add(new Table { TableNumber = $"Masa 2-{i}", FloorId = 2 });
            }

            // Floor 3
            for (int i = 1; i <= 10; i++)
            {
                tables.Add(new Table { TableNumber = $"Masa 3-{i}", FloorId = 3 });
            }

            context.Tables.AddRange(tables);
            context.SaveChanges();
        }
    }
}
