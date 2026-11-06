using Common.Permissions;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using HSCrm.WebApi.Controllers;
using Framework.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace HSCrm.UnitTests;

public class TenantSecurityRegressionTests
{
    [Fact]
    public void TenantDropdownList_MustRequireTenantViewPermission()
    {
        var method = typeof(TenantController).GetMethod(nameof(TenantController.TenantDropdownList));

        Assert.NotNull(method);
        Assert.Contains(
            method!.GetCustomAttributes(typeof(PermissionAttribute), inherit: true)
                .Cast<PermissionAttribute>(),
            attribute => attribute.Policy == $"Permission:{Permissions.Tenants.View}");
    }

    [Fact]
    public async Task TenantRole_CannotReceivePlatformTenantPermissions()
    {
        var currentUser = new MutableCurrentUserService
        {
            TenantId = 2,
            UserId = "tenant-user",
            IsAuthenticated = true
        };
        var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 2 };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("TenantPermissionBoundary_" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
        context.Tenants.Add(new Tenant
        {
            Id = 2,
            TenantCode = "TENANT02",
            TenantTitle = "Tenant 2",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Roles.Add(new ApplicationRole
        {
            Id = "tenant-role-2",
            Name = "Accountant",
            NormalizedName = "ACCOUNTANT",
            RoleFaName = "حسابدار",
            TenantId = 2
        });
        context.Permissions.Add(new Permission
        {
            Name = Permissions.Tenants.View,
            Title = "مشاهده شرکت‌ها",
            CategoryTitleEn = "Tenants",
            CategoryTitleFa = "شرکت‌ها",
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        });
        await context.SaveChangesAsync();

        var service = new RoleService(context, null!, currentUser);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignPermissionsToRoleAsync("tenant-role-2", new List<string>
            {
                Permissions.Tenants.View
            }));
    }
}
