using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.DataModel.Services.Repository
{
    public class ProjectAccessService : IProjectAccessService
    {
        private readonly ApplicationDbContext _context;

        public ProjectAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> HasFullProjectAccessAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsTenantOwner, u.UserType })
                .FirstOrDefaultAsync(ct);

            if (user == null)
                return false;

            if (user.IsTenantOwner || user.UserType == 1 || user.UserType == 4 || user.UserType == 110)
                return true;

            var roleNames = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.Name)
                .ToListAsync(ct);

            return roleNames.Any(r =>
                string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "Owner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<int>> GetAccessibleProjectIdsAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            if (await HasFullProjectAccessAsync(userId, ct))
            {
                return await _context.Projects
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(p => p.TenantId == tenantId && !p.IsDeleted)
                    .Select(p => p.Id)
                    .ToListAsync(ct);
            }

            return await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(pm => pm.TenantId == tenantId && pm.UserId == userId && pm.IsActive && !pm.IsDeleted)
                .Select(pm => pm.ProjectId)
                .Distinct()
                .ToListAsync(ct);
        }

        public async Task<bool> CanAccessProjectAsync(int tenantId, string userId, int projectId, CancellationToken ct = default)
        {
            if (projectId <= 0 || string.IsNullOrWhiteSpace(userId))
                return false;

            if (await HasFullProjectAccessAsync(userId, ct))
                return true;

            return await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .AnyAsync(pm => pm.TenantId == tenantId
                             && pm.ProjectId == projectId
                             && pm.UserId == userId
                             && pm.IsActive
                             && !pm.IsDeleted, ct);
        }

        public async Task<List<ProjectMember>> GetUserMembershipsAsync(int tenantId, string userId, CancellationToken ct = default)
        {
            return await _context.ProjectMembers
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(pm => pm.TenantId == tenantId && pm.UserId == userId && pm.IsActive && !pm.IsDeleted)
                .ToListAsync(ct);
        }
    }
}
