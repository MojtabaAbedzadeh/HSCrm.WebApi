using HSCrm.Common.Permissions;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services.Repository
{
    public class RoleService : IRoleService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentUserService _currentUser;

        public RoleService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
        {
            _context = context;
            _userManager = userManager;
            _currentUser = currentUser;
        }

        public async Task<List<ApplicationRole>> GetRolesAsync()
        {
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue) return new List<ApplicationRole>();

            // This list is used for tenant-level role management and assignment.
            // Shared system roles (including Owner) are intentionally not assignable here.
            return await _context.Roles
                .Where(r => r.TenantId == tenantId.Value &&
                    !(r.TenantId == 1 &&
                      new[] { "Admin", "Owner", "Customer", "User" }
                          .Contains(r.Name)))
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<ApplicationRole> CreateRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.");

            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue) throw new UnauthorizedAccessException("Current tenant is not available.");

            var normalizedName = roleName.Trim().ToUpperInvariant();
            // Identity's NormalizedName unique index is global, not tenant-scoped.
            var exists = await _context.Roles.AnyAsync(r => r.NormalizedName == normalizedName);
            if (exists)
                throw new InvalidOperationException("Role name is already used in the system.");

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName.Trim(),
                NormalizedName = normalizedName,
                RoleFaName = roleName.Trim(),
                TenantId = tenantId.Value
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return role;
        }

        public async Task<bool> UpdateRoleAsync(string roleId, string roleName)
        {
            var tenantId = _currentUser.TenantId;
            var role = tenantId.HasValue
                ? await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId.Value)
                : null;
            if (role == null)
                return false;

            if (IsProtectedSystemRole(role))
                throw new InvalidOperationException("Protected system roles cannot be modified.");

            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.");

            var normalizedName = roleName.Trim().ToUpperInvariant();
            var duplicate = await _context.Roles.AnyAsync(r =>
                r.Id != role.Id && r.NormalizedName == normalizedName);
            if (duplicate)
                throw new InvalidOperationException("Role name is already used in the system.");

            role.Name = roleName.Trim();
            role.NormalizedName = normalizedName;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteRoleAsync(string roleId)
        {
            var tenantId = _currentUser.TenantId;
            var role = tenantId.HasValue
                ? await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId.Value)
                : null;
            if (role == null)
                return false;

            if (IsProtectedSystemRole(role))
                throw new InvalidOperationException("Protected system roles cannot be deleted.");

            if (await _context.UserRoles.AnyAsync(ur => ur.RoleId == roleId))
                throw new InvalidOperationException("A role assigned to users cannot be deleted.");

            var rolePermissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(rolePermissions);
            _context.Roles.Remove(role);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<string>> GetRolePermissionsAsync(string roleId)
        {
            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue) return new List<string>();

            return await (
                from rp in _context.RolePermissions
                join role in _context.Roles on rp.RoleId equals role.Id
                join p in _context.Permissions on rp.PermissionId equals p.Id
                where rp.RoleId == roleId && role.TenantId == tenantId.Value
                    && p.CategoryTitleEn != PermissionScope.PlatformCategory
                select p.Name
            ).Distinct().ToListAsync();
        }

        public async Task AssignPermissionsToRoleAsync(string roleId, List<string> permissionNames)
        {
            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentException("RoleId is required.");

            permissionNames ??= new List<string>();

            var tenantId = _currentUser.TenantId;
            var roleExists = tenantId.HasValue && await _context.Roles.AnyAsync(r => r.Id == roleId && r.TenantId == tenantId.Value);
            if (!roleExists)
                throw new InvalidOperationException("Role not found.");

            var requestedNames = permissionNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var permissionIds = await _context.Permissions
                .Where(p => requestedNames.Contains(p.Name)
                    && p.CategoryTitleEn != PermissionScope.PlatformCategory)
                .Select(p => p.Id)
                .ToListAsync();

            if (permissionIds.Count != requestedNames.Count)
                throw new InvalidOperationException("One or more requested permissions do not exist or are not assignable to tenant roles.");

            var existing = await _context.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .ToListAsync();

            _context.RolePermissions.RemoveRange(existing);

            var newMappings = permissionIds.Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });

            await _context.RolePermissions.AddRangeAsync(newMappings);
            await _context.SaveChangesAsync();
        }

        private static bool IsProtectedSystemRole(ApplicationRole role) =>
            role.TenantId == 1 &&
            new[] { "Admin", "Owner", "Customer", "User" }
                .Contains(role.Name, StringComparer.OrdinalIgnoreCase);

        public async Task AssignRoleToUserAsync(string userId, string roleId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId is required.");

            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentException("RoleId is required.");

            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue)
                throw new UnauthorizedAccessException("Current tenant is not available.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId.Value);
            // Shared system roles carry global privileges and are never assignable through this tenant endpoint.
            var role = await _context.Roles.FirstOrDefaultAsync(r =>
                r.Id == roleId && r.TenantId == tenantId.Value
                && !(r.TenantId == 1 && new[] { "Admin", "Owner", "Customer", "User" }.Contains(r.Name)));

            if (user == null)
                throw new InvalidOperationException("User not found in the current tenant.");
            if (user.IsTenantOwner)
                throw new InvalidOperationException("The tenant owner account cannot be reassigned to another role.");
            if (role == null)
                throw new InvalidOperationException("Role not found or is a protected system role.");

            // Identity's EF store and this service share the scoped ApplicationDbContext.
            // Keep removal of old roles and insertion of the new role in one SQL transaction:
            // UserManager persists its changes before returning from RemoveFromRolesAsync.
            // EF InMemory does not support transactions, so retain its normal unit-test path.
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync()
                : null;

            try
            {
                // Do not early-return when the requested role already exists: a legacy
                // Owner assignment must also be removed.
                var currentUserRoles = await _userManager.GetRolesAsync(user);
                if (currentUserRoles.Any())
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentUserRoles);
                    if (!removeResult.Succeeded)
                        throw new InvalidOperationException(
                            "Could not remove the user's previous roles: " +
                            string.Join(" | ", removeResult.Errors.Select(x => x.Description)));
                }

                _context.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = roleId
                });

                await _context.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();

                throw;
            }
        }
    }
}