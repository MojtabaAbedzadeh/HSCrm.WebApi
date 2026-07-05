using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public int? TenantId
    {
        get
        {
            var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
            Console.WriteLine("Tenant from CurrentUserService: " + tenantClaim);
            if (int.TryParse(tenantClaim, out var tenantId))
                return tenantId;

            return null;
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}