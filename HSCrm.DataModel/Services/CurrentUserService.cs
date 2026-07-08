using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace HSCrm.DataModel.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        public int? FiscalYearId => int.TryParse(_httpContextAccessor.HttpContext?.User?.FindFirst("FiscalYearId")?.Value, out var id) ? id : null;
        public int? TenantId
        {
            get
            {
                var tenantClaim = _httpContextAccessor.HttpContext?.User?.FindFirst("TenantId")?.Value;
                if (int.TryParse(tenantClaim, out var tenantId))
                {
                    return tenantId;
                }
                return null;
            }
        }
        public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }
}