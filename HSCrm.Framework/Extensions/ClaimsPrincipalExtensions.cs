using System.Security.Claims;

namespace HSCrm.Framework.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public static int? GetTenantId(this ClaimsPrincipal user)
        {
            var tenantIdValue = user.FindFirst("TenantId")?.Value;

            if (int.TryParse(tenantIdValue, out var tenantId))
                return tenantId;

            return null;
        }

        public static string? GetUserName(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Name);
        }

        public static string? GetPhoneNumber(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.MobilePhone);
        }

        public static string? GetEmail(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Email);
        }
    }
}
