using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Entities.Models.Dto;
using HSCrm.Services.Tools.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _token;
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _iuwcontext;
        private readonly IPermissionService _permissionService;

        public AccountController(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, SignInManager<ApplicationUser> signInManager, IJwtTokenGenerator token, ApplicationDbContext context, IPermissionService permissionService, IJwtTokenGenerator jwtTokenGenerator, IUnitOfWork iuwcontext)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _token = token;
            _permissionService = permissionService;
            _jwtTokenGenerator = jwtTokenGenerator;
            _iuwcontext = iuwcontext;
        }

        [HttpPost("RegisterTenant")]
        public async Task<IActionResult> RegisterTenant(RegisterTenantDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userExists = await _userManager.Users.AnyAsync(x => x.UserName == model.UserName || x.Email == model.Email);

            if (userExists)
                return BadRequest("کاربری با این مشخصات قبلا ثبت شده است");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ✅ 1. ساخت Tenant
                var tenant = new Tenant
                {
                    TenantTitle = model.CompanyName,
                    TenantCode = TenantCodeGenerator.Generate(model.CompanyName),
                    CompanyPhone = model.CompanyPhone,
                    CompanyEmail = model.CompanyEmail,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();

                // ✅ 2. ساخت خودکار سال مالی (FiscalYear) داینامیک
                var persianCalendar = new System.Globalization.PersianCalendar();
                int currentYear = persianCalendar.GetYear(DateTime.Now);

                var defaultFiscalYear = new FiscalYear
                {
                    TenantId = tenant.Id,
                    Title = $"سال مالی {currentYear}",
                    // آغاز سال: اول فروردین همان سال
                    StartDate = persianCalendar.ToDateTime(currentYear, 1, 1, 0, 0, 0, 0),
                    // پایان سال: آخر اسفند همان سال (با در نظر گرفتن سال کبیسه یا عادی)
                    EndDate = persianCalendar.ToDateTime(currentYear, 12, 29, 23, 59, 59, 0),
                    IsCurrent = true,
                    IsClosed = false,
                    CreatedAt = DateTime.Now
                };

                _context.FiscalYears.Add(defaultFiscalYear);
                await _context.SaveChangesAsync(); // ذخیره سال مالی در همان تراکنش


                // ✅ 3. ساخت Owner User
                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserFirstName = model.FirstName,
                    UserLastName = model.LastName,
                    TenantId = tenant.Id,
                    IsTenantOwner = true,
                    UserStatus = true,
                    UserType = 4,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(result.Errors);
                }


                // ✅ 4. دادن رول ثابت Owner
                var roleExists = await _roleManager.RoleExistsAsync("Owner");

                if (!roleExists)
                {
                    await transaction.RollbackAsync();
                    return BadRequest("Role 'Owner' در سیستم وجود ندارد. Seeder اجرا نشده است.");
                }

                await _userManager.AddToRoleAsync(user, "Owner");


                // ✅ 5. Commit Transaction
                await transaction.CommitAsync();


                // ✅ 6. تولید توکن
                var token = await _jwtTokenGenerator.GenerateTokenAsync(user);

                return Ok(new
                {
                    Token = token,
                    TenantId = tenant.Id,
                    TenantCode = tenant.TenantCode
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, ex.Message);
            }
        }

        [Route("check-user")]
        [HttpPost]
        public async Task<IActionResult> CheckUser(UserLoginModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // ۱. پیدا کردن کاربر
            var user = await _userManager.Users.SingleOrDefaultAsync(u => u.UserName == model.UserName, ct);
            if (user == null)
                return Unauthorized("Invalid Login Info.");

            // ۲. بررسی کلمه عبور
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            if (!result.Succeeded)
                return Unauthorized("Invalid Login Info.");

            // ۳. واکشی سال‌های مالی مربوط به Tenant به صورت مستقیم و بدون اعمال فیلترهای عمومی (جهت کارایی بهتر)
            var allFiscalYears = _iuwcontext.fiscalYearUW
                .Get(f => f.TenantId == user.TenantId)
                .IgnoreQueryFilters()
                .OrderByDescending(f => f.Id)
                .ToList(); // واکشی به حافظه برای محاسبات امن و بدون خطای دیتابیس

            // تبدیل به مدل خروجی دراپ‌داون
            var fiscalYears = allFiscalYears.Select(f => new
            {
                drId = f.Id,
                drName = f.Title
            }).ToList();

            // پیدا کردن سال مالی جاری در حافظه (برای جلوگیری از خطاهای احتمالی LINQ-to-Entities)
            var currentFiscalYear = allFiscalYears.FirstOrDefault(f => f.IsCurrent);

            byte fiscalYearStatus = 10; // پیش‌فرض: نامشخص

            if (currentFiscalYear != null)
            {
                // مقایسه تاریخ‌ها در حافظه (C# Memory) به صورت امن 
                var today = DateTime.Today;
                var endDate = currentFiscalYear.EndDate.Date;

                if (currentFiscalYear.IsCurrent && endDate >= today)
                {
                    fiscalYearStatus = 0; // جاری و معتبر
                }
                else if (currentFiscalYear.IsCurrent && endDate < today)
                {
                    fiscalYearStatus = 1; // جاری اما منقضی شده
                }
                else
                {
                    fiscalYearStatus = 2; // غیرفعال
                }
            }
            else
            {
                // 💡 مدیریت خطا: اگر سال مالی جاری تعریف نشده بود، فرآیند را بلاک نکنیم 
                // تا کاربر (به خصوص نقش‌های ادمین/Owner) بتواند وارد شده و سال مالی جدید ایجاد کند.
                fiscalYearStatus = 2;
            }

            // ۴. دریافت نقش‌ها و دسترسی‌ها
            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);

            // ۵. تولید توکن JWT
            var token = await _token.GenerateTokenAsync(user, ct);

            return Ok(new
            {
                Token = token,
                FirstName = user.UserFirstName,
                LastName = user.UserLastName,
                user.UserImageUrl,
                user.UserName,
                Roles = roles,
                Permissions = permissions,
                FiscalYearStatus = fiscalYearStatus,
                Success = true,
                user.TenantId,
                UserId = user.Id,
                FiscalYears = fiscalYears
            });
        }

        [Route("login")]
        [HttpPost]
        public async Task<IActionResult> Login(UserLoginModel model, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var user = await _userManager.Users.SingleOrDefaultAsync(u => u.UserName == model.UserName);

                if (user == null)
                    return Unauthorized("Invalid Login Info.");

                var result = await _signInManager
                    .CheckPasswordSignInAsync(user, model.Password, false);

                if (!result.Succeeded)
                    return Unauthorized("Invalid Login Info.");

                // ✅ بررسی اینکه حتما FiscalYear ارسال شده باشد
                if (model.FiscalYearId == 0)
                    return BadRequest("Fiscal Year is required.");

                // ✅ حتما باید مربوط به همان Tenant باشد (خیلی مهم امنیتی)
                var fiscalYear = _iuwcontext.fiscalYearUW
                    .Get(f => f.Id == model.FiscalYearId && f.TenantId == user.TenantId)
                    .SingleOrDefault();

                if (fiscalYear == null)
                    return BadRequest("Invalid Fiscal Year.");

                byte fiscalYearStatus = 10;

                if (fiscalYear.IsCurrent && fiscalYear.EndDate.Date >= DateTime.Now)
                    fiscalYearStatus = 0;
                else if (fiscalYear.IsCurrent && fiscalYear.EndDate.Date < DateTime.Now)
                    fiscalYearStatus = 1;
                else
                    fiscalYearStatus = 2;

                var roles = await _userManager.GetRolesAsync(user);
                var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);

                var userToken = new UserJwtToken
                {
                    Token = await _token.GenerateTokenAsync(user, ct),
                    UserId = user.Id,
                    FirstName = user.UserFirstName,
                    LastName = user.UserLastName,
                    UserImageUrl = user.UserImageUrl,
                    TenantId = user.TenantId.ToString(),
                    UserName = user.UserName,
                    Roles = roles,
                    Permissions = permissions,
                    FiscalYearStatus = fiscalYearStatus
                };

                return Ok(userToken);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}