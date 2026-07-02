using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pharmacy.Core.Entities;

namespace Pharmacy.Infrastructure.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(PharmacyDbContext context)
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed Users if empty
            if (!await context.Users.AnyAsync())
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = "admin",
                    PasswordHash = HashPassword("admin123"),
                    FullName = "Quản trị hệ thống",
                    Role = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                var staffUser = new User
                {
                    Id = Guid.NewGuid(),
                    Username = "staff",
                    PasswordHash = HashPassword("staff123"),
                    FullName = "Dược sĩ Nguyễn Văn A",
                    Role = "Staff",
                    CreatedAt = DateTime.UtcNow
                };

                await context.Users.AddRangeAsync(adminUser, staffUser);
            }

            // Seed Supplier if empty
            Guid defaultSupplierId = Guid.Empty;
            if (!await context.Suppliers.AnyAsync())
            {
                var supplier = new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = "Tổng công ty Dược phẩm Trung ương I",
                    ContactPerson = "Nguyễn Văn B",
                    Phone = "0901234567",
                    Address = "160 Tôn Đức Thắng, Đống Đa, Hà Nội",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Suppliers.AddAsync(supplier);
                defaultSupplierId = supplier.Id;
            }
            else
            {
                var existingSupplier = await context.Suppliers.FirstOrDefaultAsync();
                if (existingSupplier != null)
                {
                    defaultSupplierId = existingSupplier.Id;
                }
            }

            // Seed Products & ProductBatches if empty
            if (!await context.Products.AnyAsync() && defaultSupplierId != Guid.Empty)
            {
                var products = new List<Product>
                {
                    new Product { Id = Guid.Parse("47a285d8-c9c0-43eb-b8f2-89bd36cb47a3"), SupplierId = defaultSupplierId, Name = "Bơm 10ml", Category = "Thiết bị y tế", Strength = "10ml", Unit = "Cái", ImportPrice = 1200, SellingPrice = 3000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("2f41d994-df73-455b-800a-4fb48d7c9a91"), SupplierId = defaultSupplierId, Name = "Bơm 5ml", Category = "Thiết bị y tế", Strength = "5ml", Unit = "Cái", ImportPrice = 1000, SellingPrice = 3000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("9d6c3748-0cf8-4d51-83d8-a83d29a50ef1"), SupplierId = defaultSupplierId, Name = "Cefixime 200mg", ActiveIngredient = "Cefixime", Category = "Thuốc", DosageForm = "Viên nén", Strength = "200mg", PrescriptionRequired = true, Unit = "Viên", ImportPrice = 3200, SellingPrice = 5000, MinStockLevel = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("ea684d0b-6072-46a4-9275-6e012cdbfcb2"), SupplierId = defaultSupplierId, Name = "Dao cắt chỉ", Category = "Thiết bị y tế", Unit = "Cái", ImportPrice = 950, SellingPrice = 10000, MinStockLevel = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("955a12bd-7d1a-4d2c-8ab5-0453de69b828"), SupplierId = defaultSupplierId, Name = "Dây truyền dịch", Category = "Thiết bị y tế", Unit = "Bịch", ImportPrice = 4000, SellingPrice = 5000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("bc6bdf77-3e11-47cc-9818-b7eb4196144e"), SupplierId = defaultSupplierId, Name = "Effe 500mg", ActiveIngredient = "Paracetamol", Category = "Thuốc", DosageForm = "Viên sủi", Strength = "500mg", Unit = "Viên", ImportPrice = 3000, SellingPrice = 5000, MinStockLevel = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("dfdfb2b5-e6a3-485c-a5b6-c567954d2417"), SupplierId = defaultSupplierId, Name = "Fegamide", Category = "Thuốc", DosageForm = "Ống", Unit = "Ống", ImportPrice = 24000, SellingPrice = 40000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("f44ccae6-6df3-4c91-a1e6-df05b1876543"), SupplierId = defaultSupplierId, Name = "Kim bướm (xanh + vàng)", Category = "Thiết bị y tế", Unit = "Cái", ImportPrice = 1200, SellingPrice = 5000, MinStockLevel = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("75c02b1f-e9b4-4b53-b27b-fb8e5c1a7d60"), SupplierId = defaultSupplierId, Name = "Kim châm cứu", Category = "Thiết bị y tế", Unit = "Cái", ImportPrice = 320, SellingPrice = 1000, MinStockLevel = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("25cde6cf-1bfb-4f9f-8647-38e2172778cd"), SupplierId = defaultSupplierId, Name = "Lidocain ống", ActiveIngredient = "Lidocaine", Category = "Thuốc", DosageForm = "Ống tiêm", Strength = "2%", PrescriptionRequired = true, Unit = "Hộp", ImportPrice = 700, SellingPrice = 10000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("ad9e334a-9b16-43b6-bfb2-60197d10f882"), SupplierId = defaultSupplierId, Name = "Medrokort 40 (solu nội)", ActiveIngredient = "Methylprednisolone", Category = "Thuốc", DosageForm = "Lọ bột pha tiêm", Strength = "40mg", PrescriptionRequired = true, Unit = "Lọ", ImportPrice = 28000, SellingPrice = 60000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("ca67c541-e129-4e78-9e58-f9bde2f91df5"), SupplierId = defaultSupplierId, Name = "Methylprednisolon 16mg", ActiveIngredient = "Methylprednisolone", Category = "Thuốc", DosageForm = "Viên nén", Strength = "16mg", PrescriptionRequired = true, Unit = "Viên", ImportPrice = 870, SellingPrice = 2000, MinStockLevel = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("5f89be2a-13a8-4e1b-90f7-ebf423ab11f8"), SupplierId = defaultSupplierId, Name = "Nacl 0.9%", ActiveIngredient = "Natri Clorid", Category = "Thuốc", DosageForm = "Chai truyền", Strength = "0.9%", Unit = "Chai", ImportPrice = 12000, SellingPrice = 30000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("fcd86f4a-fa13-4df4-8d4e-e17918a5cb16"), SupplierId = defaultSupplierId, Name = "Ngải cứu", Category = "Thuốc", Unit = "Cái", ImportPrice = 6500, SellingPrice = 10000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("ae9f18a2-25de-4b13-a4c0-7fbe8b5a17e0"), SupplierId = defaultSupplierId, Name = "Nước cất", ActiveIngredient = "Nước cất pha tiêm", Category = "Thuốc", Unit = "Viên", ImportPrice = 900, SellingPrice = 2000, MinStockLevel = 50, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("6d3fbcd7-4eb0-4d43-85b2-32bc8fb91d2c"), SupplierId = defaultSupplierId, Name = "Piracetam 1g", ActiveIngredient = "Piracetam", Category = "Thuốc", DosageForm = "Ống tiêm", Strength = "1g", PrescriptionRequired = true, Unit = "Ống", ImportPrice = 8000, SellingPrice = 30000, MinStockLevel = 15, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("5e8acbf0-22c6-48eb-a1d2-bc324c5678a9"), SupplierId = defaultSupplierId, Name = "Ringerlactat", ActiveIngredient = "Ringer Lactate", Category = "Thuốc", DosageForm = "Chai truyền", Strength = "500ml", Unit = "Chai", ImportPrice = 12000, SellingPrice = 30000, MinStockLevel = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("8d867c4f-c0d1-4e7b-944a-d1e9f1a234b6"), SupplierId = defaultSupplierId, Name = "Ugo (Gạc mỡ)", Category = "Thuốc", DosageForm = "Miếng gạc", Unit = "Miếng", ImportPrice = 45000, SellingPrice = 55000, MinStockLevel = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("11cde23f-e145-4de6-91b4-2cdbf72c78a3"), SupplierId = defaultSupplierId, Name = "Test cúm", Category = "Thiết bị y tế", DosageForm = "Khay thử", Unit = "Test", ImportPrice = 38000, SellingPrice = 50000, MinStockLevel = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("cb8fd2d9-1b5e-4df6-8cd4-5fbe674d89a2"), SupplierId = defaultSupplierId, Name = "Katrypsin (Alphachymo) 5mg", ActiveIngredient = "Alphachymotrypsin", Category = "Thuốc", DosageForm = "Viên nén", Strength = "5mg", Unit = "Viên", ImportPrice = 700, SellingPrice = 1000, MinStockLevel = 100, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("59fe1cd7-ef4c-45a8-ac3d-0cbdf13d52c1"), SupplierId = defaultSupplierId, Name = "Cerebrolysin", ActiveIngredient = "Cerebrolysin peptit", Category = "Thuốc", DosageForm = "Ống tiêm", Strength = "10ml", PrescriptionRequired = true, Unit = "Ống", ImportPrice = 115000, SellingPrice = 120000, MinStockLevel = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("44fde16d-318e-4a6c-9411-fbde8cb45e6f"), SupplierId = defaultSupplierId, Name = "Tanganil", ActiveIngredient = "Acetylleucine", Category = "Thuốc", DosageForm = "Ống tiêm", Strength = "500mg/5ml", PrescriptionRequired = true, Unit = "Ống", ImportPrice = 21000, SellingPrice = 60000, MinStockLevel = 5, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                    new Product { Id = Guid.Parse("98bfcd84-bfe1-4c12-8abf-4cdbf781a95e"), SupplierId = defaultSupplierId, Name = "Kim lấy thuốc", Category = "Thiết bị y tế", Unit = "Cái", ImportPrice = 1000, SellingPrice = 2000, MinStockLevel = 20, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
                };

                var batches = new List<ProductBatch>
                {
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[0].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 47, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[1].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 55, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[2].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 90, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[3].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 67, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[4].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 32, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[5].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 9, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[6].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 16, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[7].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 16, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[8].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 2880, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[9].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 64, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[10].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 36, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[11].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 19, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[12].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 28, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[13].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 75, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[14].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 29, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[15].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 28, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[16].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 21, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[17].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 2, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[18].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 0, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[19].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 290, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[20].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 10, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[21].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 5, CreatedAt = DateTime.UtcNow },
                    new ProductBatch { Id = Guid.NewGuid(), ProductId = products[22].Id, BatchNumber = "LOT-INITIAL", ExpirationDate = DateTime.UtcNow.AddYears(2), CurrentQuantity = 2, CreatedAt = DateTime.UtcNow }
                };

                await context.Products.AddRangeAsync(products);
                await context.ProductBatches.AddRangeAsync(batches);
            }

            // Seed Customers if empty
            if (!await context.Customers.AnyAsync())
            {
                var c1 = new Customer
                {
                    Id = Guid.NewGuid(),
                    FullName = "Nguyễn Thị Hoa",
                    Phone = "0987654321",
                    AllergyNotes = "Không dị ứng",
                    RewardPoints = 120,
                    CreatedAt = DateTime.UtcNow
                };

                var c2 = new Customer
                {
                    Id = Guid.NewGuid(),
                    FullName = "Trần Văn Hùng",
                    Phone = "0912345678",
                    AllergyNotes = "Dị ứng với Amoxicillin (Penicillin)",
                    RewardPoints = 45,
                    CreatedAt = DateTime.UtcNow
                };

                await context.Customers.AddRangeAsync(c1, c2);
            }

            await context.SaveChangesAsync();
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
