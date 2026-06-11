namespace HSCrm.DataModel.Services.Interface
{
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(string userId, string permissionName);
        Task<List<string>> GetUserPermissionsAsync(string userId);
    }
}
