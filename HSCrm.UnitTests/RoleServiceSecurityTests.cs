using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HSCrm.UnitTests
{
    public class RoleServiceSecurityTests
    {
        [Fact]
        public async Task AssignRoleToUserAsync_RejectsTenantOwnerAccount()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("tenant-owner-role-" + Guid.NewGuid().ToString("N"))
                .Options;

            var currentUser = new MutableCurrentUserService
            {
                TenantId = 7,
                UserId = "tenant-admin",
                IsAuthenticated = true
            };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 12 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Users.Add(new ApplicationUser
            {
                Id = "tenant-owner",
                UserName = "tenant-owner",
                NormalizedUserName = "TENANT-OWNER",
                UserFirstName = "Tenant",
                UserLastName = "Owner",
                TenantId = 7,
                IsTenantOwner = true,
                UserStatus = true,
                UserType = 0
            });
            context.Roles.Add(new ApplicationRole
            {
                Id = "ordinary-role",
                Name = "Accountant",
                NormalizedName = "ACCOUNTANT",
                RoleFaName = "حسابدار",
                TenantId = 7
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);

            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignRoleToUserAsync("tenant-owner", "ordinary-role"));

            userManager.Verify(
                manager => manager.RemoveFromRolesAsync(
                    It.IsAny<ApplicationUser>(),
                    It.IsAny<IEnumerable<string>>()),
                Times.Never);
            Assert.Empty(await context.UserRoles.ToListAsync());
        }

        [Fact]
        public async Task AssignRoleToUserAsync_RejectsSharedOwnerRole_EvenForCentralTenant()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("owner-role-assignment-" + Guid.NewGuid().ToString("N"))
                .Options;

            var currentUser = new MutableCurrentUserService
            {
                TenantId = 1,
                UserId = "tenant-admin",
                IsAuthenticated = true
            };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Users.Add(new ApplicationUser
            {
                Id = "target-user",
                UserName = "target-user",
                NormalizedUserName = "TARGET-USER",
                UserFirstName = "Test",
                UserLastName = "User",
                TenantId = 1,
                UserStatus = true,
                UserType = 0
            });
            context.Roles.Add(new ApplicationRole
            {
                Id = "shared-owner-role",
                Name = "Owner",
                NormalizedName = "OWNER",
                RoleFaName = "مدیر",
                TenantId = 1
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);

            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignRoleToUserAsync("target-user", "shared-owner-role"));

            userManager.Verify(
                manager => manager.GetRolesAsync(It.IsAny<ApplicationUser>()),
                Times.Never);
            Assert.Empty(await context.UserRoles.ToListAsync());
        }

        [Fact]
        public async Task UpdateRoleAsync_RejectsProtectedSystemRole()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("protected-role-update-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Roles.Add(new ApplicationRole
            {
                Id = "system-admin",
                Name = "Admin",
                NormalizedName = "ADMIN",
                RoleFaName = "مدیر سیستم",
                TenantId = 1
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateRoleAsync("system-admin", "RenamedAdmin"));

            Assert.Equal("Admin", (await context.Roles.FindAsync("system-admin"))!.Name);
        }

        [Fact]
        public async Task DeleteRoleAsync_RejectsProtectedSystemRole()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("protected-role-delete-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Roles.Add(new ApplicationRole
            {
                Id = "system-owner",
                Name = "Owner",
                NormalizedName = "OWNER",
                RoleFaName = "مالک",
                TenantId = 1
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteRoleAsync("system-owner"));

            Assert.NotNull(await context.Roles.FindAsync("system-owner"));
        }

        [Fact]
        public async Task DeleteRoleAsync_RejectsRoleAssignedToUsers()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("assigned-role-delete-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 7, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Users.Add(new ApplicationUser
            {
                Id = "employee",
                UserName = "employee",
                NormalizedUserName = "EMPLOYEE",
                UserFirstName = "Test",
                UserLastName = "Employee",
                TenantId = 7,
                UserStatus = true
            });
            context.Roles.Add(new ApplicationRole
            {
                Id = "accountant",
                Name = "Accountant",
                NormalizedName = "ACCOUNTANT",
                RoleFaName = "حسابدار",
                TenantId = 7
            });
            context.UserRoles.Add(new UserRole { UserId = "employee", RoleId = "accountant" });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.DeleteRoleAsync("accountant"));

            Assert.NotNull(await context.Roles.FindAsync("accountant"));
            Assert.Single(await context.UserRoles.ToListAsync());
        }

        [Fact]
        public async Task AssignPermissionsToRoleAsync_RejectsUnknownPermissionWithoutChangingAssignments()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("unknown-permission-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 7, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Roles.Add(new ApplicationRole
            {
                Id = "accountant",
                Name = "Accountant",
                NormalizedName = "ACCOUNTANT",
                RoleFaName = "حسابدار",
                TenantId = 7
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AssignPermissionsToRoleAsync("accountant", new List<string> { "Permission.That.Does.Not.Exist" }));

            Assert.Empty(await context.RolePermissions.ToListAsync());
        }

        [Fact]
        public async Task CreateRoleAsync_RejectsNameAlreadyUsedByAnotherTenant()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("global-role-name-create-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 7, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Roles.Add(new ApplicationRole
            {
                Id = "tenant-one-role",
                Name = "Accountant",
                NormalizedName = "ACCOUNTANT",
                RoleFaName = "حسابدار",
                TenantId = 1
            });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateRoleAsync("accountant"));

            Assert.Single(await context.Roles.ToListAsync());
        }

        [Fact]
        public async Task UpdateRoleAsync_RejectsNameAlreadyUsedByAnotherTenant()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("global-role-name-update-" + Guid.NewGuid().ToString("N"))
                .Options;
            var currentUser = new MutableCurrentUserService { TenantId = 7, UserId = "admin", IsAuthenticated = true };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Roles.AddRange(
                new ApplicationRole
                {
                    Id = "tenant-seven-role",
                    Name = "Sales",
                    NormalizedName = "SALES",
                    RoleFaName = "فروش",
                    TenantId = 7
                },
                new ApplicationRole
                {
                    Id = "tenant-one-role",
                    Name = "Accountant",
                    NormalizedName = "ACCOUNTANT",
                    RoleFaName = "حسابدار",
                    TenantId = 1
                });
            await context.SaveChangesAsync();

            var userManager = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null!, null!, null!, null!, null!, null!, null!, null!);
            var service = new RoleService(context, userManager.Object, currentUser);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.UpdateRoleAsync("tenant-seven-role", "accountant"));

            Assert.Equal("Sales", (await context.Roles.FindAsync("tenant-seven-role"))!.Name);
        }
    }
}
