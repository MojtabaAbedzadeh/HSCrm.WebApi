using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HSCrm.UnitTests;

public class PermissionServiceSecurityTests
{
    private static async Task<(ApplicationDbContext Context, PermissionService Service, string UserId)> CreateContextAsync(
        int tenantId, bool isTenantOwner)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);

        var fiscalYear = new Mock<ICurrentFiscalYearService>();
        fiscalYear.SetupGet(x => x.FiscalYearId).Returns(1);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("PermissionOwner_" + Guid.NewGuid().ToString("N"))
            .Options;

        var context = new ApplicationDbContext(options, currentUser.Object, fiscalYear.Object);
        // Materialize model seed data (including the shared Owner role) before arranging the test tenant.
        await context.Database.EnsureCreatedAsync();

        // The model's central tenant seed is intentionally inactive by default.
        // Activate it in this authorization fixture to exercise the central-owner path.
        if (tenantId == 1)
        {
            var centralTenant = await context.Tenants.IgnoreQueryFilters()
                .SingleAsync(tenant => tenant.Id == 1);
            centralTenant.IsActive = true;
        }

        if (tenantId != 1)
        {
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                TenantCode = "TENANT-" + tenantId,
                TenantTitle = "Test tenant",
                IsActive = true,
                IsDeleted = false
            });
        }

        var user = new ApplicationUser
        {
            Id = "user-" + Guid.NewGuid().ToString("N"),
            UserName = "test-user",
            NormalizedUserName = "TEST-USER",
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            UserFirstName = "Test",
            UserLastName = "User",
            TenantId = tenantId,
            IsTenantOwner = isTenantOwner,
            UserStatus = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        context.Users.Add(user);

        var ownerRole = await context.Roles.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.Id == "OwnerId");

        // InMemory EnsureCreated does not consistently materialize Identity seed rows
        // across provider/runtime versions; arrange the shared system role explicitly.
        if (ownerRole == null)
        {
            ownerRole = new ApplicationRole
            {
                Id = "OwnerId",
                Name = "Owner",
                RoleFaName = "مدیر",
                NormalizedName = "OWNER",
                ConcurrencyStamp = "OwnerId",
                TenantId = 1
            };
            context.Roles.Add(ownerRole);
        }
        var permission = new Permission
        {
            Name = "owner.privileged.test",
            Title = "Owner privileged test",
            CategoryTitleEn = "Tests",
            CategoryTitleFa = "آزمایش"
        };
        var platformPermission = new Permission
        {
            Name = "tenants.view",
            Title = "View tenants",
            CategoryTitleEn = "Tenants",
            CategoryTitleFa = "شرکت‌ها"
        };
        context.Permissions.AddRange(permission, platformPermission);
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = ownerRole.Id });
        context.RolePermissions.AddRange(
            new RolePermission { RoleId = ownerRole.Id, Permission = permission },
            new RolePermission { RoleId = ownerRole.Id, Permission = platformPermission });

        await context.SaveChangesAsync();
        return (context, new PermissionService(context), user.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SharedOwnerRole_DoesNotGrantPermissionsToNonOwnerAccounts(int tenantId)
    {
        var (context, service, userId) = await CreateContextAsync(tenantId, isTenantOwner: false);
        await using (context)
        {
            Assert.False(await service.HasPermissionAsync(userId, "owner.privileged.test"));
            Assert.Empty(await service.GetUserPermissionsAsync(userId));
        }
    }

    [Fact]
    public async Task SharedOwnerRole_GrantsPermissionsToProvisionedTenantOwner()
    {
        var (context, service, userId) = await CreateContextAsync(tenantId: 2, isTenantOwner: true);
        await using (context)
        {
            Assert.True(await service.HasPermissionAsync(userId, "owner.privileged.test"));
            Assert.Contains("owner.privileged.test", await service.GetUserPermissionsAsync(userId));
            Assert.False(await service.HasPermissionAsync(userId, "tenants.view"));
            Assert.DoesNotContain("tenants.view", await service.GetUserPermissionsAsync(userId));
        }
    }

    [Fact]
    public async Task CentralTenantOwner_CanUsePlatformTenantPermissions()
    {
        var (context, service, userId) = await CreateContextAsync(tenantId: 1, isTenantOwner: true);
        await using (context)
        {
            Assert.True(await service.HasPermissionAsync(userId, "tenants.view"));
            Assert.Contains("tenants.view", await service.GetUserPermissionsAsync(userId));
        }
    }

    [Fact]
    public async Task NonCentralTenantOwner_CannotUsePlatformTenantPermissions()
    {
        var (context, service, userId) = await CreateContextAsync(tenantId: 2, isTenantOwner: true);
        await using (context)
        {
            Assert.False(await service.HasPermissionAsync(userId, "tenants.view"));
            Assert.DoesNotContain("tenants.view", await service.GetUserPermissionsAsync(userId));
        }
    }
}
