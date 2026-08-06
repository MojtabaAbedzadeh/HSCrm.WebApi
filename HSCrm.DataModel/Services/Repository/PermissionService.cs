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
            return await _context.UserRoles
                .Include(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .AnyAsync(rp => rp.Permission.Name == permissionName);
        }

        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            // استخراج تمام نام‌های Permission برای یک کاربر به صورت Distinct
            return await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Name)
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