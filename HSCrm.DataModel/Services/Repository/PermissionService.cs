using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services.Repository
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;

        public PermissionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasPermissionAsync(string userId, string permissionName)
        {
            var tenantId = await _context.Users
                .Where(user => user.Id == userId)
                .Select(user => (int?)user.TenantId)
                .SingleOrDefaultAsync();

            if (!tenantId.HasValue)
                return false;

            return await _context.UserRoles
                .Where(userRole => userRole.UserId == userId
                    && userRole.User.UserStatus
                    && userRole.User.TenantId == tenantId.Value
                    && userRole.User.Tenant.IsActive
                    && !userRole.User.Tenant.IsDeleted
                    && (
                        // A shared Owner role is privileged even for users in tenant 1.
                        // Only the account explicitly provisioned as the tenant owner may use it.
                        (userRole.Role.TenantId == tenantId.Value
                            && (userRole.Role.Name != "Owner" || userRole.User.IsTenantOwner))
                        || (userRole.Role.TenantId == 1
                            && userRole.Role.Name == "Owner"
                            && userRole.User.IsTenantOwner)))
                .SelectMany(userRole => userRole.Role.RolePermissions)
                .AnyAsync(rolePermission => rolePermission.Permission.Name == permissionName);
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var tenantId = await _context.Users
                .Where(user => user.Id == userId)
                .Select(user => (int?)user.TenantId)
                .SingleOrDefaultAsync();

            if (!tenantId.HasValue)
                return new List<string>();

            return await _context.UserRoles
                .Where(userRole => userRole.UserId == userId
                    && userRole.User.UserStatus
                    && userRole.User.TenantId == tenantId.Value
                    && userRole.User.Tenant.IsActive
                    && !userRole.User.Tenant.IsDeleted
                    && (
                        // A shared Owner role is privileged even for users in tenant 1.
                        // Only the account explicitly provisioned as the tenant owner may use it.
                        (userRole.Role.TenantId == tenantId.Value
                            && (userRole.Role.Name != "Owner" || userRole.User.IsTenantOwner))
                        || (userRole.Role.TenantId == 1
                            && userRole.Role.Name == "Owner"
                            && userRole.User.IsTenantOwner)))
                .SelectMany(userRole => userRole.Role.RolePermissions)
                .Select(rolePermission => rolePermission.Permission.Name)
                .Distinct()
                .ToListAsync();
        }

        public async Task AssignAllPermissionsToRoleAsync(string roleName, int tenantId)
        {
            // نقش مخصوص tenant را پیدا کن
            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Name == roleName && r.TenantId == tenantId);

            if (role == null)
                throw new Exception("Role not found for this tenant.");

            // تمام permissionهای موجود (permission چند-tenant نیست)
            var permissions = await _context.Permissions.ToListAsync();

            if (!permissions.Any())
                return;

            // تمام دسترسی‌های فعلی این role
            var existingPermissionIds = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            // فقط permissionهای جدید
            var newRolePermissions = permissions
                .Where(p => !existingPermissionIds.Contains(p.Id))
                .Select(p => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = p.Id
                })
                .ToList();

            if (newRolePermissions.Any())
            {
                await _context.RolePermissions.AddRangeAsync(newRolePermissions);
                await _context.SaveChangesAsync();
            }
        }
    }
}