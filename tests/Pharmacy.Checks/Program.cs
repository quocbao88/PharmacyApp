using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pharmacy.Core.DTOs;
using Pharmacy.Core.Exceptions;
using Pharmacy.Core.Rules;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Repositories;
using Pharmacy.Infrastructure.Services;

// This executable creates a new database on a dedicated loopback test server only.
// It never reads application configuration or touches an existing application database.
const string server = "Host=127.0.0.1;Port=55439;Username=pharmacy_test;Database=postgres;Timeout=5";
var databaseName = "pharmacy_checks_" + Guid.NewGuid().ToString("N");
await using (var connection = new NpgsqlConnection(server))
{
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("CREATE DATABASE " + databaseName, connection);
    await command.ExecuteNonQueryAsync();
}
var connectionString = new NpgsqlConnectionStringBuilder(server) { Database = databaseName }.ConnectionString;
var userId = Guid.NewGuid();
var supplierId = Guid.NewGuid();
var productId = Guid.NewGuid();
var batchId = Guid.NewGuid();
var expiry = new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var checks = 0;

PharmacyDbContext Open()
{
    return new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>().UseNpgsql(connectionString).Options);
}

OrderService Orders(PharmacyDbContext context)
{
    return new OrderService(context, new OrderRepository(context), new ProductRepository(context),
        new CustomerRepository(context), NullLogger<OrderService>.Instance, new NoHttp(), new ConfigurationBuilder().Build());
}

void Check(bool value, string name)
{
    if (!value)
    {
        throw new Exception("FAIL " + name);
    }
    checks++;
    Console.WriteLine("PASS " + name);
}

async Task Reject<T>(Func<Task> action, string name) where T : Exception
{
    try
    {
        await action();
    }
    catch (T)
    {
        Check(true, name);
        return;
    }
    throw new Exception("Expected " + typeof(T).Name + ": " + name);
}

await using (var context = Open())
{
    // Create the legacy schema, add an existing lot, then exercise the forward migration.
    await context.GetService<IMigrator>().MigrateAsync("20260717233539_AddGuestInfoToOrder");
    var now = DateTime.UtcNow;
    var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("local-test-only")));
    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users (id,username,password_hash,full_name,role,created_at) VALUES ({userId},{"test"},{hash},{"Test"},{"Admin"},{now})");
    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO suppliers (id,name,created_at) VALUES ({supplierId},{"Test"},{now})");
    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO products (id,supplier_id,name,unit,import_price,selling_price,min_stock_level,prescription_required,created_at,updated_at) VALUES ({productId},{supplierId},{"Test medicine"},{"Tablet"},{2m},{12m},{5},{false},{now},{now})");
    await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO product_batches (id,product_id,batch_number,expiration_date,current_quantity,created_at) VALUES ({batchId},{productId},{"LEGACY"},{expiry},{20},{now})");
    await context.Database.MigrateAsync();
    var batch = await context.ProductBatches.SingleAsync();
    Check(batch.CurrentQuantity == 20 && batch.UnitCost == 2, "Migration preserves legacy quantities and snapshots unit cost");
    Check(await context.StockMovements.CountAsync(x => x.Type == "Opening" && x.Quantity == 20) == 1,
        "Migration records one explicit opening balance");
    context.ProductUnitConversions.Add(new Pharmacy.Core.Entities.ProductUnitConversion
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        UnitName = "Box",
        ConversionValue = 10,
        SellingPrice = 90
    });
    await context.SaveChangesAsync();
}

var receipt = new CreateBatchRequest { RequestId = Guid.NewGuid(), BatchNumber = "LEGACY", ExpirationDate = expiry, Quantity = 30, ImportPrice = 4 };
async Task Receive()
{
    await using var context = Open();
    await new InventoryService(context).ReceiveAsync(productId, receipt, userId);
}
await Task.WhenAll(Receive(), Receive());
await using (var context = Open())
{
    var batch = await context.ProductBatches.SingleAsync();
    Check(batch.CurrentQuantity == 50 && batch.UnitCost == 3.2m, "Concurrent receipt retry adds stock once and keeps one lot");
    Check(await context.StockMovements.CountAsync(x => x.Type == "Receipt") == 1, "Receipt is idempotent");
}

var checkout = new CheckoutOrderRequest
{
    RequestId = Guid.NewGuid(),
    PaymentMethod = "Cash",
    DiscountAmount = 20,
    Items = new() { new() { ProductId = productId, Quantity = 2, SoldUnit = "Box" } }
};
Guid orderId;
await using (var context = Open())
{
    var order = await Orders(context).CheckoutAsync(userId, checkout);
    orderId = order.Id;
    Check(order.TotalAmount == 160 && order.Details.Single().CostPrice == 32, "Checkout snapshots selected unit price and actual lot cost");
}
await using (var context = Open())
{
    var repeated = await Orders(context).CheckoutAsync(userId, checkout);
    Check(repeated.Id == orderId && await context.ProductBatches.Select(x => x.CurrentQuantity).SingleAsync() == 30,
        "Checkout retry returns original order without deducting stock again");
}
async Task<Guid?> CompetingCheckout()
{
    await using var context = Open();
    try
    {
        var result = await Orders(context).CheckoutAsync(userId, new CheckoutOrderRequest
        {
            RequestId = Guid.NewGuid(), PaymentMethod = "Cash",
            Items = new() { new() { ProductId = productId, Quantity = 20 } }
        });
        return result.Id;
    }
    catch (InsufficientStockException)
    {
        return null;
    }
}
var competing = await Task.WhenAll(CompetingCheckout(), CompetingCheckout());
Check(competing.Count(x => x.HasValue) == 1, "Concurrent sales cannot oversell the remaining lot");
var competingId = competing.Single(x => x.HasValue)!.Value;

Guid detailId;
await using (var context = Open())
{
    detailId = await context.OrderDetails.Where(x => x.OrderId == orderId).Select(x => x.Id).SingleAsync();
}
var returned = new ReturnOrderRequest
{
    RequestId = Guid.NewGuid(), Reason = "Customer returned one box",
    Items = new() { new() { OrderDetailId = detailId, Quantity = 1, Restock = true } }
};
await using (var context = Open())
{
    var result = await Orders(context).ReturnOrderAsync(orderId, returned, userId);
    Check(result.Status == "PartiallyReturned" && result.RefundedAmount == 80, "Partial return refunds discounted amount and marks state");
    Check(await context.ProductBatches.Select(x => x.CurrentQuantity).SingleAsync() == 20, "Return restores base units to the original lot");
}
await using (var context = Open())
{
    await Orders(context).ReturnOrderAsync(orderId, returned, userId);
    Check(await context.ProductBatches.Select(x => x.CurrentQuantity).SingleAsync() == 20, "Return retry is idempotent");
}
await using (var context = Open())
{
    var result = await Orders(context).ReturnOrderAsync(orderId, new ReturnOrderRequest
    {
        RequestId = Guid.NewGuid(), Reason = "Damaged returned box",
        Items = new() { new() { OrderDetailId = detailId, Quantity = 1, Restock = false } }
    }, userId);
    Check(result.Status == "Returned" && result.RefundedAmount == 160, "Final return refunds the exact remaining discounted amount");
    Check(await context.ProductBatches.Select(x => x.CurrentQuantity).SingleAsync() == 20, "Damaged returns do not increase sellable stock");
}
await using (var context = Open())
{
    await Reject<PharmacyValidationException>(() => Orders(context).ReturnOrderAsync(orderId, NewReturnRequest(), userId), "Cannot return more than was sold");
}
ReturnOrderRequest NewReturnRequest()
{
    return new ReturnOrderRequest
    {
        RequestId = Guid.NewGuid(), Reason = "Excess return",
        Items = new() { new() { OrderDetailId = detailId, Quantity = 1 } }
    };
}
async Task<bool> Cancel()
{
    await using var context = Open();
    try
    {
        await Orders(context).CancelOrderAsync(competingId, "Entry mistake", userId);
        return true;
    }
    catch (PharmacyValidationException)
    {
        return false;
    }
}
var cancellations = await Task.WhenAll(Cancel(), Cancel());
Check(cancellations.Count(x => x) == 1, "Concurrent cancellation restores stock once");
await using (var context = Open())
{
    var service = new InventoryService(context);
    await service.AdjustAsync(batchId, new StockAdjustmentRequest { Quantity = 42, ExpectedQuantity = 40, Reason = "Physical stock count" }, userId);
    Check(await context.ProductBatches.Select(x => x.CurrentQuantity).SingleAsync() == 42, "Count adjustment updates stock and writes a movement");
}
await using (var context = Open())
{
    await Reject<InsufficientStockException>(() => new InventoryService(context).AdjustAsync(batchId,
        new StockAdjustmentRequest { Quantity = 42, ExpectedQuantity = 40, Reason = "Stale count" }, userId), "Stale count cannot overwrite concurrent stock changes");
}
await using (var context = Open())
{
    var service = new InventoryService(context);
    var report = await service.GetReportAsync(BusinessDate.Today, BusinessDate.Today);
    var row = report.Items.Single();
    Check(row.OpeningQuantity + row.ReceivedQuantity - row.SoldQuantity + row.ReturnedQuantity + row.AdjustedQuantity == row.ClosingQuantity,
        "Stock report reconciles opening + receipts - sales + returns + adjustments");
    Check(row.ClosingQuantity == 42 && row.ClosingValue == 134.4m, "Report closing quantity and value match the actual stock journal");
    Check(row.RevenueAmount == 0 && row.CostAmount == 32, "Report includes refunds and retains damaged-goods cost");
    var summary = await new ReportService(new OrderRepository(context)).GetRevenueAndProfitReportAsync(BusinessDate.Today, BusinessDate.Today);
    Check(summary.TotalRevenue == 0 && summary.TotalCost == 32, "Dashboard summary subtracts refunds and retains damaged-goods cost");
    await Reject<PharmacyValidationException>(() => service.GetReportAsync(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31)), "Report refuses to invent pre-cutover history");
    Check((await service.GetHistoryAsync(batchId)).Count >= 7, "Lot history includes every stock and return event");
    Check(await context.Orders.Select(x => x.OrderCode).Distinct().CountAsync() == await context.Orders.CountAsync(), "Concurrent invoices have unique codes");
}
Console.WriteLine($"All {checks} PostgreSQL integration checks passed. Isolated database: {databaseName}");
Directory.CreateDirectory("artifacts/InventoryTests");
await File.WriteAllTextAsync("artifacts/InventoryTests/session.json", System.Text.Json.JsonSerializer.Serialize(new { databaseName, userId, productId, batchId }));

sealed class NoHttp : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        throw new InvalidOperationException("Integration checks must not call external services.");
    }
}
