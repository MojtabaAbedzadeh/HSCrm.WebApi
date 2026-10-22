using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace HSCrm.Dashboard.Controllers
{
    public class BaseController : Controller
    {
        protected readonly IConfiguration _config;

        public BaseController(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// این متد به عنوان یک فیلتر، قبل از اجرای هر اکشنی به صورت خودکار اجرا می‌شود
        /// و ViewBagها (از جمله سال مالی فعال، وضعیت سال مالی، توکن و شناسه تننت) را برای تمام لایه‌ها و ویوها پر می‌کند.
        /// </summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);

            if (User != null)
            {
                ViewBag.ApiAddress = _config["ApiAddress"];
                ViewBag.UserId = User.FindFirstValue("UserId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
                ViewBag.Token = User.FindFirstValue("Token");
                ViewBag.FirstName = User.FindFirstValue("UserFirstName") ?? User.FindFirstValue("FirstName");
                ViewBag.LastName = User.FindFirstValue("UserLastName") ?? User.FindFirstValue("LastName");
                ViewBag.TenantId = User.FindFirstValue("TenantId");
                ViewBag.UserImageUrl = User.FindFirstValue("UserImageUrl");
                ViewBag.FiscalYearId = User.FindFirstValue("FiscalYearId") ?? Request.Cookies["ActiveFiscalYearId"] ?? "0";
                ViewBag.FiscalYearTitle = User.FindFirstValue("FiscalYearTitle") ?? "سال مالی جاری";
                ViewBag.FiscalYearStatus = User.FindFirstValue("FiscalYearStatus") ?? "0";
                ViewBag.Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("Email");
                ViewBag.Mobile = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("MobilePhone");
            }
        }
    }
}
