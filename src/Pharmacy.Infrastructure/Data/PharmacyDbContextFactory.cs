using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pharmacy.Infrastructure.Data
{
    // Schema tooling must not boot the application or run its startup migrations/seeding.
    public class PharmacyDbContextFactory : IDesignTimeDbContextFactory<PharmacyDbContext>
    {
        public PharmacyDbContext CreateDbContext(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable("PHARMACY_DESIGN_CONNECTION") ??
                "Host=127.0.0.1;Database=pharmacy_design";
            return new PharmacyDbContext(new DbContextOptionsBuilder<PharmacyDbContext>()
                .UseNpgsql(connection).Options);
        }
    }
}
