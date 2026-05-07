using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _token;
        private readonly IUnitOfWork _context;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IJwtTokenGenerator token, IUnitOfWork context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _token = token;
            _context = context;
        }

        [Route("login")]
        [HttpPost]
        public async Task<IActionResult> Login(UserLoginModel model)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);

                var user = await _userManager.Users.SingleOrDefaultAsync(u => u.UserName == model.UserName);
                if (user == null) return Unauthorized("Invalid Login Info.");

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
                if (!result.Succeeded) return Unauthorized("Invalid Login Info.");

                var roles = await _userManager.GetRolesAsync(user);

                var fiscalYear = _context.fiscalYearUW.Get(f => f.Id == model.FiscalYearId).SingleOrDefault();
                byte fiscalYearStatus = 10;

                if (fiscalYear.IsCurrent == true && fiscalYear.EndDate.Date >= DateTime.Now)
                {
                    // سال مالی صحیح می باشد
                    fiscalYearStatus = 0;
                }
                else if (fiscalYear.IsCurrent == true && fiscalYear.EndDate.Date < DateTime.Now)
                {
                    //سال مالی باز است ولی تاریخ سال مالی از تاریخ روز عبور کرده است.
                    fiscalYearStatus = 1;
                }
                else if (fiscalYear.IsCurrent == false)
                {
                    //سال مالی بسته شده است
                    fiscalYearStatus = 2;
                }

                var userToken = new UserJwtToken
                {
                    UserId = user.Id,
                    FirstName = user.UserFirstName,
                    LastName = user.UserLastName,
                    UserImageUrl = user.UserImageUrl,
                    TenantId = user.TenantId.ToString(),
                    UserName = user.UserName,
                    Roles = await _userManager.GetRolesAsync(user),
                    FiscalYearStatus = fiscalYearStatus,
                    Token = await _token.CreateTokenAsync(user)
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
