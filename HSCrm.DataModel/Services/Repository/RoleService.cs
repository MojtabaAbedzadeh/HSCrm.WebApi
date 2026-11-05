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

            return await _context.Roles
                .Where(r => r.TenantId == tenantId.Value || (r.TenantId == 1 && r.Name == "Owner"))
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<ApplicationRole> CreateRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.");

            var tenantId = _currentUser.TenantId;
            if (!tenantId.HasValue) throw new UnauthorizedAccessException("Current tenant is not available.");

            var exists = await _context.Roles.AnyAsync(r => r.TenantId == tenantId.Value && r.Name == roleName);
            if (exists)
                throw new InvalidOperationException("Role already exists.");

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant(),
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

            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.");

            role.Name = roleName;
            role.NormalizedName = roleName.ToUpperInvariant();

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

            var permissionIds = await _context.Permissions
                .Where(p => permissionNames.Contains(p.Name))
                .Select(p => p.Id)
                .ToListAsync();

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

            // Replace all current assignments. Do not early-return when the requested role
            // already exists: a legacy Owner assignment must also be removed.
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
        }
    }
}