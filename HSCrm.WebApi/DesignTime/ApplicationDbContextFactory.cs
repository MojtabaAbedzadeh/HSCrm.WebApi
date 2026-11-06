using HSCrm.DataModel.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HSCrm.WebApi.DesignTime;

/// <summary>
/// Creates ApplicationDbContext for EF tooling without completing API startup
/// or executing the application's database seeding routine.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Migration discovery and SQL generation do not open a database connection.
        var connectionString =
            Environment.GetEnvironmentVariable("HSCrm__DesignTimeConnectionString")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=HSCrmDesignTime;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Tenant/fiscal-year services are only needed when application queries execute.
        return new ApplicationDbContext(options, null!, null!);
    }
}
