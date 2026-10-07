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
        private const string DefaultAvatarPath = "/Upload/UserImage/defaultAvatar.png";

        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _token;
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _iuwcontext;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenGenerator token,
            ApplicationDbContext context,
            IPermissionService permissionService,
            IUnitOfWork iuwcontext,
            ILogger<AccountController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _token = token;
            _permissionService = permissionService;
            _iuwcontext = iuwcontext;
            _logger = logger;
        }

        [HttpPost("RegisterTenant")]
        public async Task<IActionResult> RegisterTenant(RegisterTenantDto model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userExists = await _userManager.Users.AnyAsync(
                x => x.UserName == model.UserName || x.Email == model.Email, ct);

            if (userExists)
                return BadRequest("کاربری با این مشخصات قبلا ثبت شده است");

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            try
            {
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
                await _context.SaveChangesAsync(ct);

                var persianCalendar = new System.Globalization.PersianCalendar();
                int currentYear = persianCalendar.GetYear(DateTime.Now);
                int lastDayOfEsfand = persianCalendar.GetDaysInMonth(currentYear, 12);

                var defaultFiscalYear = new FiscalYear
                {
                    TenantId = tenant.Id,
                    Title = $"سال مالی {currentYear}",
                    StartDate = persianCalendar.ToDateTime(currentYear, 1, 1, 0, 0, 0, 0),
                    EndDate = persianCalendar.ToDateTime(currentYear, 12, lastDayOfEsfand, 23, 59, 59, 0),
                    IsCurrent = true,
                    IsClosed = false,
                    CreatedAt = DateTime.Now
                };

                _context.FiscalYears.Add(defaultFiscalYear);
                await _context.SaveChangesAsync(ct);

                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserFirstName = model.FirstName,
                    UserLastName = model.LastName,
                    TenantId = tenant.Id,
                    UserImageUrl = BuildDefaultAvatarUrl(),
                    IsTenantOwner = true,
                    UserStatus = true,
                    UserType = 4,
                    EmailConfirmed = false,
                    LockoutEnabled = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return BadRequest(result.Errors);
                }

                if (!await _roleManager.RoleExistsAsync("Owner"))
                {
                    await transaction.RollbackAsync(ct);
                    return BadRequest("Role 'Owner' در سیستم وجود ندارد. Seeder اجرا نشده است.");
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "Owner");
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return BadRequest(roleResult.Errors);
                }

                await transaction.CommitAsync(ct);

                var token = await _token.GenerateTokenAsync(user, defaultFiscalYear.Id, ct);

                return Ok(new
                {
                    Token = token,
                    TenantId = tenant.Id,
                    TenantCode = tenant.TenantCode,
                    FiscalYearId = defaultFiscalYear.Id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error registering tenant for user {UserName}", model.UserName);
                return StatusCode(500, "Internal server error.");
            }
        }

        [Route("check-user")]
        [HttpPost]
        public async Task<IActionResult> CheckUser(UserLoginModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.Users
                .Include(u => u.Tenant)
                .SingleOrDefaultAsync(u => u.UserName == model.UserName, ct);

            var authError = await ValidateCredentialsAsync(user, model.Password);
            if (authError != null)
                return authError;

            var allFiscalYears = await _iuwcontext.fiscalYearUW
                .Get(f => f.TenantId == user!.TenantId)
                .IgnoreQueryFilters()
                .OrderByDescending(f => f.Id)
                .ToListAsync(ct);

            var fiscalYears = allFiscalYears.Select(f => new
            {
                drId = f.Id,
                drName = f.Title
            }).ToList();

            var currentFiscalYear = allFiscalYears.FirstOrDefault(f => f.IsCurrent);
            var fiscalYearStatus = ResolveFiscalYearStatus(currentFiscalYear);

            var roles = await _userManager.GetRolesAsync(user!);
            var permissions = await _permissionService.GetUserPermissionsAsync(user!.Id);

            // Pre-login token: no FiscalYear claim until Login selects a valid year
            var token = await _token.GenerateTokenAsync(user, null, ct);

            return Ok(new
            {
                Token = token,
                TenantId = user.TenantId,
                UserId = user.Id,
                UserName = user.UserName,
                UserFirstName = user.UserFirstName,
                UserLastName = user.UserLastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                UserImageUrl = user.UserImageUrl,
                UserStatus = user.UserStatus,
                UserType = user.UserType,
                FiscalYearStatus = fiscalYearStatus,
                Roles = roles,
                Permissions = permissions,
                FiscalYears = fiscalYears,
                Success = true
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

                if (model.FiscalYearId <= 0)
                    return BadRequest("Fiscal Year is required.");

                var user = await _userManager.Users
                    .Include(u => u.Tenant)
                    .SingleOrDefaultAsync(u => u.UserName == model.UserName, ct);

                var authError = await ValidateCredentialsAsync(user, model.Password);
                if (authError != null)
                    return authError;

                var fiscalYear = await _iuwcontext.fiscalYearUW
                    .Get(f => f.Id == model.FiscalYearId && f.TenantId == user!.TenantId)
                    .IgnoreQueryFilters()
                    .SingleOrDefaultAsync(ct);

                if (fiscalYear == null)
                    return BadRequest("Invalid Fiscal Year.");

                if (fiscalYear.IsClosed)
                    return BadRequest("Fiscal Year is closed.");

                var fiscalYearStatus = ResolveFiscalYearStatus(fiscalYear);
                var roles = await _userManager.GetRolesAsync(user!);
                var permissions = await _permissionService.GetUserPermissionsAsync(user!.Id);

                var userToken = new UserJwtToken
                {
                    Token = await _token.GenerateTokenAsync(user, model.FiscalYearId, ct),
                    UserId = user.Id,
                    FirstName = user.UserFirstName,
                    LastName = user.UserLastName,
                    UserImageUrl = user.UserImageUrl,
                    TenantId = user.TenantId.ToString(),
                    UserName = user.UserName,
                    Roles = roles,
                    Email = user.Email,
                    Permissions = permissions,
                    FiscalYearStatus = fiscalYearStatus,
                    UserStatus = user.UserStatus,
                    UserType = user.UserType,
                    FiscalYear = fiscalYear.Title
                };

                return Ok(userToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {UserName}", model.UserName);
                return StatusCode(500, "Internal server error.");
            }
        }

        private async Task<IActionResult?> ValidateCredentialsAsync(ApplicationUser? user, string password)
        {
            if (user == null)
                return Unauthorized("Invalid Login Info.");

            if (!user.UserStatus)
                return Unauthorized("User account is inactive.");

            if (user.Tenant == null || !user.Tenant.IsActive)
                return Unauthorized("Tenant is inactive.");

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                return StatusCode(StatusCodes.Status423Locked, "Account is temporarily locked due to failed login attempts.");

            if (!result.Succeeded)
                return Unauthorized("Invalid Login Info.");

            return null;
        }

        private static byte ResolveFiscalYearStatus(FiscalYear? fiscalYear)
        {
            if (fiscalYear == null)
                return 2;

            if (fiscalYear.IsClosed)
                return 2;

            var today = DateTime.Today;
            var endDate = fiscalYear.EndDate.Date;

            if (fiscalYear.IsCurrent && endDate >= today)
                return 0; // جاری و معتبر

            if (fiscalYear.IsCurrent && endDate < today)
                return 1; // جاری اما منقضی شده

            return 2; // غیرفعال / غیرجاری
        }

        private string BuildDefaultAvatarUrl()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host.Value}";
            return $"{baseUrl}{DefaultAvatarPath}";
        }
    }
}
