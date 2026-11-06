using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HSCrm.UnitTests;

public sealed class RoleServiceSqlServerTransactionTests
{
    [SqlServerFact]
    public async Task AssignRoleToUserAsync_RollsBackRemovalWhenAddingNewRoleFails()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("HSCrmSqlServer");
        Assert.False(string.IsNullOrWhiteSpace(baseConnectionString));

        var databaseName = "HSCrmRoleTx_" + Guid.NewGuid().ToString("N");
        var masterBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = "master"
        };

        await using (var masterConnection = new SqlConnection(masterBuilder.ConnectionString))
        {
            await masterConnection.OpenAsync();
            await using var createCommand = masterConnection.CreateCommand();
            createCommand.CommandText = $"CREATE DATABASE [{databaseName}]";
            await createCommand.ExecuteNonQueryAsync();
        }

        var databaseBuilder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };
        var currentUser = new MutableCurrentUserService
        {
            TenantId = 1,
            UserId = "test-admin",
            IsAuthenticated = true
        };
        var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(databaseBuilder.ConnectionString)
            .Options;

        try
        {
            await using (var setupContext = new ApplicationDbContext(options, currentUser, currentFiscalYear))
            {
                await setupContext.Database.EnsureCreatedAsync();
                setupContext.Users.Add(new ApplicationUser
                {
                    Id = "transaction-user",
                    UserName = "transaction-user",
                    NormalizedUserName = "TRANSACTION-USER",
                    UserFirstName = "Transaction",
                    UserLastName = "User",
                    TenantId = 1,
                    UserStatus = true,
                    UserType = 0
                });
                setupContext.Roles.AddRange(
                    new ApplicationRole
                    {
                        Id = "old-role",
                        Name = "OldRole",
                        NormalizedName = "OLDROLE",
                        RoleFaName = "نقش قبلی",
                        TenantId = 1
                    },
                    new ApplicationRole
                    {
                        Id = "new-role",
                        Name = "NewRole",
                        NormalizedName = "NEWROLE",
                        RoleFaName = "نقش جدید",
                        TenantId = 1
                    });
                setupContext.UserRoles.Add(new UserRole
                {
                    UserId = "transaction-user",
                    RoleId = "old-role"
                });
                // Deliberately pre-create the target assignment. The service's insert
                // must hit the relational primary-key constraint after removing OldRole.
                setupContext.UserRoles.Add(new UserRole
                {
                    UserId = "transaction-user",
                    RoleId = "new-role"
                });
                await setupContext.SaveChangesAsync();
            }

            await using (var serviceContext = new ApplicationDbContext(options, currentUser, currentFiscalYear))
            {
                var store = Mock.Of<IUserStore<ApplicationUser>>();
                var userManager = new Mock<UserManager<ApplicationUser>>(
                    store, null!, null!, null!, null!, null!, null!, null!, null!);

                userManager
                    .Setup(manager => manager.GetRolesAsync(It.IsAny<ApplicationUser>()))
                    .ReturnsAsync(new List<string> { "OldRole" });

                userManager
                    .Setup(manager => manager.RemoveFromRolesAsync(
                        It.IsAny<ApplicationUser>(),
                        It.IsAny<IEnumerable<string>>()))
                    .Returns<ApplicationUser, IEnumerable<string>>(async (_, _) =>
                    {
                        var oldAssignment = await serviceContext.UserRoles.SingleAsync(
                            x => x.UserId == "transaction-user" && x.RoleId == "old-role");
                        serviceContext.UserRoles.Remove(oldAssignment);
                        await serviceContext.SaveChangesAsync();
                        return IdentityResult.Success;
                    });

                var service = new RoleService(serviceContext, userManager.Object, currentUser);

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => service.AssignRoleToUserAsync("transaction-user", "new-role"));
            }

            await using (var verificationContext = new ApplicationDbContext(options, currentUser, currentFiscalYear))
            {
                var assignments = await verificationContext.UserRoles
                    .AsNoTracking()
                    .Where(x => x.UserId == "transaction-user")
                    .Select(x => x.RoleId)
                    .OrderBy(x => x)
                    .ToListAsync();

                Assert.Equal(new[] { "new-role", "old-role" }, assignments);
            }
        }
        finally
        {
            var cleanupBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "master"
            };
            await using var cleanupConnection = new SqlConnection(cleanupBuilder.ConnectionString);
            await cleanupConnection.OpenAsync();
            await using var cleanupCommand = cleanupConnection.CreateCommand();
            cleanupCommand.CommandText = $@"IF DB_ID(N'{databaseName}') IS NOT NULL
BEGIN
    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [{databaseName}];
END";
            await cleanupCommand.ExecuteNonQueryAsync();
        }
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HSCrmSqlServer")))
            Skip = "Requires the HSCrmSqlServer SQL Server integration-test connection string.";
    }
}
