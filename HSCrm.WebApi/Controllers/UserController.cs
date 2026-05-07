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
        public async Task<ApiResponse<List<ApplicationUser>>> Get(CancellationToken ct)
        {
            var persons = (await _context.userManagerUW
                .GetAsync(u => u.UserType == 0, asNoTracking: true, ct: ct))
                .ToList();

            return new ApiResponse<List<ApplicationUser>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = persons
            };
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
        public async Task<ApiResponse<ApplicationUser>> Create([FromForm] UserCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            try
            {
                ApplicationUser user = new ApplicationUser()
                {
                    UserFirstName = model.UserFirstNameC,
                    UserLastName = model.UserLastNameC,                    
                    UserImageUrl = model.UserImageUrlC,
                    UserStatus = true,
                    UserType = 0,
                    UserGender = model.UserGenderC,
                    PhoneNumber = model.PhoneNumberC,
                    Email = model.EmailC,
                    TenantId = model.TenantId,
                    IsTenantOwner = false,
                    UserName = model.UserNameC,
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

                return new ApiResponse<ApplicationUser>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = user
                };
            }
            catch (Exception)
            {
                return new ApiResponse<ApplicationUser>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
        }

        [HttpPut("UpdateUser")]
        [ProducesResponseType(typeof(ApplicationUser), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ApiResponse<ApplicationUser>> Update([FromBody] UserEditModel model)
        {
            if (string.IsNullOrEmpty(model.UserId)) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            if (!ModelState.IsValid) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            try
            {
                var getUser = await _userManager.FindByIdAsync(model.UserId);

                if (getUser != null)
                {
                    var mapModel = _map.Map(model, getUser);
                    IdentityResult result = await _userManager.UpdateAsync(mapModel);
                    if (result.Succeeded) return new ApiResponse<ApplicationUser>
                    {
                        Status = true,
                        StatusCode = ApiResultStatusCode.Success,
                        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                        Data = getUser
                    };
                }
            }
            catch (Exception)
            {
                return new ApiResponse<ApplicationUser>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
            return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.ServerError,
                Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
            };
        }
    }
}
