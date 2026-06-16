namespace HSCrm.DataModel.Services.Interface
{
    public interface IRoleService
    {
        Task<List<ApplicationRole>> GetRolesAsync();
        Task<ApplicationRole> CreateRoleAsync(string roleName);
        Task<bool> UpdateRoleAsync(string roleId, string roleName);
        Task<bool> DeleteRoleAsync(string roleId);

        Task<List<string>> GetRolePermissionsAsync(string roleId);
        Task AssignPermissionsToRoleAsync(string roleId, List<string> permissionNames);
        Task AssignRoleToUserAsync(string userId, string roleId);
    }
}