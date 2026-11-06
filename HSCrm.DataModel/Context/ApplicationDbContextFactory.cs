using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HSCrm.DataModel.Context;

/// <summary>
/// Creates the EF Core model for tooling without bootstrapping the API host
/// or running application startup seeding.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // A real connection is not required for migration discovery or SQL script generation.
        // An explicit design-time connection string can be supplied when a tooling operation needs it.
        var connectionString =
            Environment.GetEnvironmentVariable("HSCrm__DesignTimeConnectionString")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=HSCrmDesignTime;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // These services are only consulted when tenant/fiscal-year scoped queries execute.
        // EF tooling only needs the model and migrations metadata.
        return new ApplicationDbContext(options, null!, null!);
    }
}
