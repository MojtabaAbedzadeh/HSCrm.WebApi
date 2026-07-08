using Microsoft.AspNetCore.Http;
using HSCrm.DataModel.Services.Interface;

namespace HSCrm.DataModel.Services
{
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

                // حالت اول: خواندن از هدر HTTP (بسیار رایج در SPAها و اپلیکیشن‌های حسابداری/CRM)
                if (httpContext.Request.Headers.TryGetValue("X-FiscalYear-Id", out var headerValue)
                    && int.TryParse(headerValue, out var fiscalYearIdFromHeader))
                {
                    return fiscalYearIdFromHeader;
                }

                // حالت دوم: خواندن از Claimهای توکن کاربر (اگر سال مالی را موقع لاگین درون Claims ذخیره می‌کنی)
                var fiscalYearClaim = httpContext.User?.FindFirst("FiscalYearId")?.Value;
                if (int.TryParse(fiscalYearClaim, out var fiscalYearIdFromClaim))
                {
                    return fiscalYearIdFromClaim;
                }

                return null;
            }
        }
    }
}