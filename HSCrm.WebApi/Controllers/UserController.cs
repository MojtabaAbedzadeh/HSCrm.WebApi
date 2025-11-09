using AutoMapper;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _cpntext;
        private readonly IMapper _map;

        public UserController(IUnitOfWork context, UserManager<ApplicationUser> userManager, IMapper map, ApplicationDbContext context1)
        {
            _context = context;
            _userManager = userManager;
            _map = map;
            _context = context;
        }


        [HttpGet("GetUsers")]
        public async Task<IActionResult> GetUsers(CancellationToken ct)
        {
            var persons = await _context.userManagerUW.GetAsync(asNoTracking: true, ct: ct);


            //var persons = await _hr.Persons.AsNoTracking().Take(1000).ToListAsync(ct);
            return Ok(persons);
        }

        [HttpGet("GetUserById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApplicationUser), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(string userId)
        {
            var request = _context.userManagerUW.GetById(userId);

            if (request == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
            };

            return new ApiResponse<ApplicationUser>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = request
            };
        }

        [HttpPost("CreateUser")]
        public async Task<IActionResult> Create([FromForm] UserCreateModel model)
        {
            if (!ModelState.IsValid) return BadRequest(model);

            try
            {
                ApplicationUser user = new ApplicationUser()
                {
                    UserFirstName = model.UserFirstName,
                    UserLastName = model.UserLastName,
                    PhoneNumber = model.PhoneNumber,
                    UserGender = model.UserGender,
                    UserName = model.UserName,
                    UserImageUrl = model.UserImageUrl,
                    UserStatus = false,
                    UserType = model.UserType,

                };
                IdentityResult result = await _userManager.CreateAsync(user, "123456");

                if (result.Succeeded)
                {
                    if (user.UserType == 1)
                    {
                        await _userManager.AddToRoleAsync(user, "Admin");
                    }
                    else
                    {
                        await _userManager.AddToRoleAsync(user, "user");
                    }
                }

                return StatusCode(200);
            }
            catch (Exception)
            {
                return BadRequest(500);
            }
        }

        [HttpPut("UpdateUser")]
        [ProducesResponseType(typeof(ApplicationUser), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromForm] UserEditModel model)
        {
            if (string.IsNullOrEmpty(model.UserId)) return BadRequest();

            if (!ModelState.IsValid) return BadRequest(model);

            try
            {
                var getUser = await _userManager.FindByIdAsync(model.UserId);
                if (getUser != null)
                {
                    var mapModel = _map.Map(model, getUser);
                    IdentityResult result = await _userManager.UpdateAsync(mapModel);
                    if (result.Succeeded)
                    {
                        return Ok("200");
                    }
                }
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
            return StatusCode(500);
        }
    }
}
