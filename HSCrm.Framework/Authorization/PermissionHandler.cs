using HSCrm.DataModel.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Framework.Authorization;

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var claims = context.User.Claims.Select(c => $"{c.Type} = {c.Value}").ToList();
        if (string.IsNullOrWhiteSpace(userId))
            return; // یا لاگ بزن / یا شکست صریح

        var hasPermission = await _permissionService.HasPermissionAsync(userId, requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);
    }    
}
