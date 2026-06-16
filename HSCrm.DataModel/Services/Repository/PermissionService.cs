using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;
using System;

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
    }
}
