using HSCrm.BussinessLogic.PublicMethod;
using HSCrm.Dashboard.Services.Interface;
using HSCrm.Models.ModelDto;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Automation.Dashboard.Controllers
{
    public class AccountController : Controller
    {
        private readonly ILoginService _login;
        private readonly IRegisterService _registerService;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly GetListApi _getListApi;

        public AccountController(ILoginService login, IMemoryCache cache, IConfiguration config, IRegisterService registerService, GetListApi getListApi)
        {
            _login = login;
            _cache = cache;
            _config = config;
            _registerService = registerService;
            _getListApi = getListApi;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
            {
                return Redirect("/AdminArea/Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterTenantModel model)
        {
            var result = await _registerService.Register(model);

            if (!result.Success)
            {
                ViewBag.ErrorMessage = result.Message;
                return View(model);
            }

            // ساخت Claims
            var claims = new List<Claim>
            {
                new Claim("Token", result.Token),
                new Claim("TenantId", result.TenantId),
                new Claim("UserId", result.UserId),
                new Claim(ClaimTypes.NameIdentifier, result.UserId),
                new Claim(ClaimTypes.Name, model.UserName),
                new Claim(ClaimTypes.Email, model.Email)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var properties = new AuthenticationProperties { IsPersistent = true };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

            return Redirect("/AdminArea/Home/Index");
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            if (HttpContext.User.Identity != null && HttpContext.User.Identity.IsAuthenticated)
            {
                return Redirect("/AdminArea/Home");
            }

            string apiUrl = "FiscalYear/FiscalYearDropdownList";

            string jsonFullModel = await _getListApi.GetApiList(apiUrl);

            var jsonDataParse = JsonConvert.DeserializeObject<dynamic>(jsonFullModel);

            ViewBag.FiscalsYearList = JsonConvert.DeserializeObject<List<FiscalYearDropDown>>(jsonDataParse.data.ToString());

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            var result = await _login.Login(model);

            if (result == null)
            {
                ViewBag.ErrorMessage = "خطا در برقراری ارتباط نهایی.";
                return View();
            }

            // ایجاد کل Claimهای مورد نیاز فرانت از روی اطلاعات واقعی کاربر
            var claims = new List<Claim>
    {
        new Claim("Token", result.Token ?? ""),
        new Claim("TenantId", result.TenantId.ToString()),
        new Claim("FiscalYearId", model.FiscalYearId.ToString()),
        new Claim(ClaimTypes.NameIdentifier, result.UserId ?? ""),
        new Claim(ClaimTypes.Name, result.UserName ?? ""),
        new Claim("FiscalYearStatus", result.FiscalYearStatus.ToString()),
        
        // Claimهای جدید پروفایل
        new Claim("UserFirstName", result.UserFirstName ?? ""),
        new Claim("UserLastName", result.UserLastName ?? ""),
        new Claim(ClaimTypes.Email, result.Email ?? ""),
        new Claim(ClaimTypes.MobilePhone, result.PhoneNumber ?? ""),
        new Claim("UserImageUrl", result.UserImageUrl ?? "")
    };

            if (result.Roles != null)
                claims.AddRange(result.Roles.Select(r => new Claim(ClaimTypes.Role, r)));

            if (result.Permissions != null)
                claims.AddRange(result.Permissions.Select(p => new Claim("Permission", p)));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            return Redirect("/AdminArea/Home/Index");
        }

        public async Task<IActionResult> Logout()
        {
            _cache.Remove("UserInfo");

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        public async Task<IActionResult> CheckUserAndGetFiscals(string userName, string password)
        {
            // ۱. بررسی نام کاربری و رمز عبور از طریق سرویس لاگین
            var result = await _login.Login(new LoginModel { UserName = userName, Password = password });

            if (result == null)
                return Json(new { success = false, message = "نام کاربری یا رمز عبور اشتباه است." });

            // ۲. فراخوانی API برای گرفتن سال‌های مالی بر اساس TenantId کاربر
            string apiUrl = $"FiscalYear/FiscalYearDropdownList?tenantId={result.TenantId}";
            string jsonFullModel = await _getListApi.GetApiList(apiUrl);
            var jsonDataParse = JsonConvert.DeserializeObject<dynamic>(jsonFullModel);

            return Json(new
            {
                success = true,
                fiscalYears = result.FiscalYears,
                tenantId = result.TenantId,
                userId = result.UserId
            });
        }


        [HttpPost]
        public async Task<IActionResult> SwitchFiscalYear(int fiscalYearId, int fiscalYearStatus = 0, string? fiscalYearTitle = null)
        {
            if (HttpContext.User.Identity is not ClaimsIdentity currentIdentity || !currentIdentity.IsAuthenticated)
            {
                return Unauthorized(new { status = false, message = "کاربر احراز هویت نشده است." });
            }

            var claims = currentIdentity.Claims
                .Where(c => c.Type != "FiscalYearId" && c.Type != "FiscalYearStatus" && c.Type != "FiscalYearTitle")
                .ToList();

            claims.Add(new Claim("FiscalYearId", fiscalYearId.ToString()));
            claims.Add(new Claim("FiscalYearStatus", fiscalYearStatus.ToString()));
            if (!string.IsNullOrWhiteSpace(fiscalYearTitle))
            {
                claims.Add(new Claim("FiscalYearTitle", fiscalYearTitle));
            }

            Response.Cookies.Append("ActiveFiscalYearId", fiscalYearId.ToString(), new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            var newIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var newPrincipal = new ClaimsPrincipal(newIdentity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                newPrincipal,
                new AuthenticationProperties { IsPersistent = true });

            return Json(new
            {
                status = true,
                message = $"سال مالی فعال به «{fiscalYearTitle ?? fiscalYearId.ToString()}» تغییر یافت.",
                fiscalYearId,
                fiscalYearStatus,
                fiscalYearTitle
            });
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
