using Framework.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

public class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:"))
        {
            var permission = policyName.Substring("Permission:".Length);

            var policy = new AuthorizationPolicyBuilder();
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new PermissionRequirement(permission));

            return policy.Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}