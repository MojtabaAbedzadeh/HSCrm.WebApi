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

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim == null)
            return;
      
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var hasPermission = await _permissionService
            .HasPermissionAsync(userId, requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);
    }
}
