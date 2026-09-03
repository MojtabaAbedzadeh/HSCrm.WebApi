using AutoMapper;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
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
        private readonly IRoleService _roleService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _map;

        public UserController(IUnitOfWork context, UserManager<ApplicationUser> userManager, IMapper map, ApplicationDbContext dbContext, IRoleService roleService)
        {
            _context = context;
            _userManager = userManager;
            _map = map;
            _dbContext = dbContext;
            _roleService = roleService;
        }

        [HttpGet("GetUsers")]
        [Permission(Permissions.Users.View)]
        public async Task<ApiResponse<List<ApplicationUser>>> Get(CancellationToken ct)
        {
            var persons = (await _context.userManagerUW
                .GetAsync(u => u.UserType != 3 && u.UserType != 110 && u.TenantId == User.GetTenantId().Value, asNoTracking: true, ct: ct))
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
        [Permission(Permissions.Users.View)]
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
        [Permission(Permissions.Users.Create)]
        public async Task<ApiResponse<UserModelDto>> Create([FromForm] UserCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return new ApiResponse<UserModelDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            try
            {
                // بررسی تکراری بودن
                var userNameExist = await _userManager.FindByNameAsync(model.UserNameC);
                var emailExist = await _userManager.FindByEmailAsync(model.EmailC);
                var phoneExist = _userManager.Users.FirstOrDefault(x => x.PhoneNumber == model.PhoneNumberC);

                if (userNameExist != null || emailExist != null || phoneExist != null)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                    };
                }

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
                    TenantId = User.GetTenantId().Value,                    
                    IsTenantOwner = false,
                    UserName = model.UserNameC
                };

                IdentityResult result = await _userManager.CreateAsync(user, "@Omid1364");

                if (!result.Succeeded)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.ServerError,
                        Message = string.Join(" | ", result.Errors.Select(x => x.Description))
                    };
                }

                // افزودن نقش
                string roleName = user.UserType == 1 ? "Admin" : "user";
                await _userManager.AddToRoleAsync(user, roleName);

                // ✅ ساخت DTO (دیگه Entity برنمی‌گردونیم)
                var dto = new UserModelDto
                {
                    Id = user.Id,
                    UserFirstName = user.UserFirstName,
                    UserLastName = user.UserLastName,
                    UserName = user.UserName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    UserImageUrl = user.UserImageUrl,
                    UserStatus = user.UserStatus,
                    RoleName = roleName
                };

                return new ApiResponse<UserModelDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = dto
                };
            }
            catch (Exception)
            {
                return new ApiResponse<UserModelDto>
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
        [Permission(Permissions.Users.Edit)]
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

        [HttpDelete("DeleteUser")]
        [Permission(Permissions.Users.Delete)]
        public async Task<ApiResponse<ApplicationUser>> Delete(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new ApiResponse<ApplicationUser>
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

                var getUser = await _userManager.FindByIdAsync(userId);

                if (getUser != null)
                {
                    _context.userManagerUW.Delete(getUser);
                    return new ApiResponse<ApplicationUser>
                    {
                        Status = true,
                        StatusCode = ApiResultStatusCode.Success,
                        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                        Data = getUser
                    };
                }
            }
            catch
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

        [HttpGet("AssignRole")]
        [Permission(Permissions.Users.Create)]
        public async Task<IActionResult> AssignRole(string userId, string roleId)
        {
            await _roleService.AssignRoleToUserAsync(userId, roleId);
            return Ok();
        }
    }
}