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

            // ۱. اولویت با هدر است (برای تغییر سریع در داشبورد)
            if (httpContext.Request.Headers.TryGetValue("X-FiscalYear-Id", out var headerValue))
            {
                if (int.TryParse(headerValue, out var id)) return id;
            }

            // ۲. اگر در هدر نبود، از کلیم توکن بخواند
            var claimValue = httpContext.User?.FindFirst("FiscalYearId")?.Value;
            return int.TryParse(claimValue, out var fiscalYearId) ? fiscalYearId : null;
        }
    }
}