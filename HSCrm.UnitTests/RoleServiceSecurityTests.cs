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
    }
}
