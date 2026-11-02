using HSCrm.DataModel.Services.Interface;
using Microsoft.AspNetCore.Http;

public class CurrentFiscalYearService : ICurrentFiscalYearService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CurrentFiscalYearService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    public int? FiscalYearId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // Fiscal year is a security boundary. Never trust a client-controlled header.
            // Switching fiscal years must issue a token with a validated FiscalYearId claim.
            var claimValue = httpContext.User?.FindFirst("FiscalYearId")?.Value;
            return int.TryParse(claimValue, out var fiscalYearId) && fiscalYearId > 0 ? fiscalYearId : null;
        }
    }
}