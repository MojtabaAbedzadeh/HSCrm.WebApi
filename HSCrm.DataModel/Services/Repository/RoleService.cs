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
        private readonly RoleManager<ApplicationRole> _roleManager;

        public RoleService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<List<ApplicationRole>> GetRolesAsync()
        {
            return await _context.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();
        }

        public async Task<ApplicationRole> CreateRoleAsync(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                throw new ArgumentException("Role name is required.");

            var exists = await _context.Roles.AnyAsync(r => r.Name == roleName);
            if (exists)
                throw new InvalidOperationException("Role already exists.");

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return role;
        }

        public async Task<bool> UpdateRoleAsync(string roleId, string roleName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
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
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
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
            return await (
                from rp in _context.RolePermissions
                join p in _context.Permissions on rp.PermissionId equals p.Id
                where rp.RoleId == roleId
                select p.Name
            ).Distinct().ToListAsync();
        }

        public async Task AssignPermissionsToRoleAsync(string roleId, List<string> permissionNames)
        {
            if (string.IsNullOrWhiteSpace(roleId))
                throw new ArgumentException("RoleId is required.");

            permissionNames ??= new List<string>();

            var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);
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

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            var roleExists = await _context.Roles.AnyAsync(r => r.Id == roleId);

            if (!userExists)
                throw new InvalidOperationException("User not found.");

            if (!roleExists)
                throw new InvalidOperationException("Role not found.");

            var exists = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

            if (exists)
                return;

            var user = await _userManager.FindByIdAsync(userId);
            var currentUserRoles = await _userManager.GetRolesAsync(user);

            if (currentUserRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentUserRoles);
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