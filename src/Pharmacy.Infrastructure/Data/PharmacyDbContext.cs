using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pharmacy.Core.Entities;

namespace Pharmacy.Infrastructure.Data
{
    public class PharmacyDbContext : DbContext
    {
        public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Shift> Shifts => Set<Shift>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
        public DbSet<ProductUnitConversion> ProductUnitConversions => Set<ProductUnitConversion>();
        public DbSet<ProductAuditLog> ProductAuditLogs => Set<ProductAuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 10. ProductAuditLog configuration
            modelBuilder.Entity<ProductAuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Action).HasMaxLength(100);
                entity.Property(e => e.ChangedBy).HasMaxLength(200);
                entity.Property(e => e.ChangedAt);
                entity.Property(e => e.Details).HasMaxLength(2000);
            });

            // 1. User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.Property(e => e.Username).HasMaxLength(100);
                entity.Property(e => e.PasswordHash).HasMaxLength(500);
                entity.Property(e => e.FullName).HasMaxLength(200);
                entity.Property(e => e.Role).HasMaxLength(50);
            });

            // 2. Supplier configuration
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.ContactPerson).HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Address).HasMaxLength(500);
            });

            // 3. Product configuration
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.ActiveIngredient).HasMaxLength(300);
                entity.Property(e => e.Category).HasMaxLength(150);
                entity.Property(e => e.Manufacturer).HasMaxLength(200);
                entity.Property(e => e.DosageForm).HasMaxLength(100);
                entity.Property(e => e.Strength).HasMaxLength(100);
                entity.Property(e => e.StorageConditions).HasMaxLength(500);
                entity.Property(e => e.Unit).HasMaxLength(50);
                entity.Property(e => e.ImportPrice).HasPrecision(18, 2);
                entity.Property(e => e.SellingPrice).HasPrecision(18, 2);
                entity.Property(e => e.UpdatedAt);

                entity.HasOne(e => e.Supplier)
                    .WithMany(s => s.Products)
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 4. ProductBatch configuration
            modelBuilder.Entity<ProductBatch>(entity =>
            {
                entity.HasKey(e => e.Id);
                // Expiration date index is vital for FIFO sorting speed
                entity.HasIndex(e => e.ExpirationDate);
                entity.HasIndex(e => new { e.ProductId, e.ExpirationDate });
                entity.Property(e => e.BatchNumber).HasMaxLength(100);
                entity.Property(e => e.ExpirationDate);

                entity.HasOne(e => e.Product)
                    .WithMany(p => p.Batches)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Database-level constraint: current quantity in batch cannot drop below zero
                entity.HasCheckConstraint("CK_ProductBatch_Quantity", "current_quantity >= 0");
            });

            // 5. Customer configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Phone).IsUnique();
                entity.Property(e => e.FullName).HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
            });

            // 6. Order configuration (no Shift dependency)
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.OrderCode).IsUnique();
                entity.Property(e => e.OrderCode).HasMaxLength(100);
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
                entity.Property(e => e.PaymentMethod).HasMaxLength(50);
                entity.Property(e => e.CreatedAt);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Customer)
                    .WithMany()
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // 8. OrderDetail configuration
            modelBuilder.Entity<OrderDetail>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.CostPrice).HasPrecision(18, 2);
                entity.Property(e => e.Subtotal).HasPrecision(18, 2);

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.Details)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Batch)
                    .WithMany()
                    .HasForeignKey(e => e.BatchId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Database-level constraint: sold quantity must be greater than zero
                entity.HasCheckConstraint("CK_OrderDetail_Quantity", "quantity > 0");
            });

            // 9. ProductUnitConversion configuration
            modelBuilder.Entity<ProductUnitConversion>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitName).HasMaxLength(50);
                entity.Property(e => e.SellingPrice).HasPrecision(18, 2);

                entity.HasOne(e => e.Product)
                    .WithMany(p => p.UnitConversions)
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Auto-convert all PascalCase naming conventions to PostgreSQL snake_case standard
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                // Convert table name
                var tableName = entity.GetTableName();
                if (tableName != null)
                {
                    entity.SetTableName(ConvertPascalToSnake(tableName));
                }

                // Convert columns
                foreach (var property in entity.GetProperties())
                {
                    var storeObjectIdentifier = StoreObjectIdentifier.Table(entity.GetTableName() ?? "", entity.GetSchema());
                    var columnName = property.GetColumnName(storeObjectIdentifier);
                    if (columnName != null)
                    {
                        property.SetColumnName(ConvertPascalToSnake(columnName));
                    }
                }

                // Convert primary keys
                foreach (var key in entity.GetKeys())
                {
                    var keyName = key.GetName();
                    if (keyName != null)
                    {
                        key.SetName(ConvertPascalToSnake(keyName));
                    }
                }

                // Convert foreign keys
                foreach (var foreignKey in entity.GetForeignKeys())
                {
                    var constraintName = foreignKey.GetConstraintName();
                    if (constraintName != null)
                    {
                        foreignKey.SetConstraintName(ConvertPascalToSnake(constraintName));
                    }
                }

                // Convert indexes
                foreach (var index in entity.GetIndexes())
                {
                    var indexName = index.GetDatabaseName();
                    if (indexName != null)
                    {
                        index.SetDatabaseName(ConvertPascalToSnake(indexName));
                    }
                }
            }
        }

        private static string ConvertPascalToSnake(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            // Replaces patterns like "PascalCase" to "pascal_case"
            var value = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2");
            return value.ToLower();
        }
    }
}
