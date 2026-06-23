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

            var userExists = await _userManager.Users
                .AnyAsync(x => x.UserName == model.UserName || x.Email == model.Email);

            if (userExists)
                return BadRequest("کاربری با این مشخصات قبلا ثبت شده است");

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // ساخت Tenant

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

                // ساخت User

                var user = new ApplicationUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    PhoneNumber = model.PhoneNumber,
                    UserFirstName = model.FirstName,
                    UserLastName = model.LastName,
                    TenantId = tenant.Id,
                    IsTenantOwner = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(result.Errors);
                }

                await _userManager.AddToRoleAsync(user, "Owner");

                // ساخت Role مالک

                var roleName = $"Owner_{tenant.Id}";

                var role = new ApplicationRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpper(),
                    TenantId = tenant.Id
                };

                _context.Roles.Add(role);

                await _context.SaveChangesAsync();

                // اتصال User به Role

                var userRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                };

                _context.UserRoles.Add(userRole);

                await _context.SaveChangesAsync();

                // دادن همه Permission ها

                var permissions = await _context.Permissions.ToListAsync();

                foreach (var permission in permissions)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = permission.Id
                    });
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                var token = _jwtTokenGenerator.GenerateTokenAsync(user);

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


        [Route("login")]
        [HttpPost]
        public async Task<IActionResult> Login(UserLoginModel model, CancellationToken ct)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var user = await _userManager.Users.SingleOrDefaultAsync(u => u.UserName == model.UserName);

                if (user == null) return Unauthorized("Invalid Login Info.");

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
                if (!result.Succeeded) return Unauthorized("Invalid Login Info.");

                var roles = await _userManager.GetRolesAsync(user);
                var permissions = await _permissionService.GetUserPermissionsAsync(user.Id);

                var fiscalYear = _iuwcontext.fiscalYearUW.Get(f => f.Id == model.FiscalYearId).SingleOrDefault();
                byte fiscalYearStatus = 10;

                if (fiscalYear.IsCurrent == true && fiscalYear.EndDate.Date >= DateTime.Now)
                {
                    fiscalYearStatus = 0;
                }
                else if (fiscalYear.IsCurrent == true && fiscalYear.EndDate.Date < DateTime.Now)
                {
                    fiscalYearStatus = 1;
                }
                else if (fiscalYear.IsCurrent == false)
                {
                    fiscalYearStatus = 2;
                }

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
                    FiscalYearStatus = fiscalYearStatus,
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
