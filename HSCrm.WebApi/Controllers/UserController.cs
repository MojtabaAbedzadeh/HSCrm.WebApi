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
using Microsoft.EntityFrameworkCore;
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
        public async Task<ApiResponse<List<UserModelDto>>> Get(CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var persons = tenantId.HasValue
                ? await _userManager.Users.AsNoTracking()
                    .Where(u => u.TenantId == tenantId.Value && u.UserType != 3 && u.UserType != 110)
                    .Select(u => new UserModelDto
                    {
                        Id = u.Id,
                        UserFirstName = u.UserFirstName,
                        UserLastName = u.UserLastName,
                        UserName = u.UserName ?? string.Empty,
                        Email = u.Email ?? string.Empty,
                        PhoneNumber = u.PhoneNumber ?? string.Empty,
                        UserImageUrl = u.UserImageUrl ?? string.Empty,
                        UserStatus = u.UserStatus,
                        RoleName = u.UserRoles.Select(ur => ur.Role.Name).FirstOrDefault() ?? string.Empty
                    }).ToListAsync(ct)
                : new List<UserModelDto>();

            return new ApiResponse<List<UserModelDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = persons
            };
        }

        [HttpGet("GetUserById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(UserModelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Users.View)]
        public ApiResponse<UserModelDto> GetById(string userId)
        {
            var tenantId = User.GetTenantId();
            var request = tenantId.HasValue ? _context.userManagerUW.Get(u => u.Id == userId && u.TenantId == tenantId.Value).FirstOrDefault() : null;

            if (request == null) return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
            };

            return new ApiResponse<UserModelDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = ToUserDto(request)
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

                if (string.IsNullOrWhiteSpace(model.PasswordC))
                    return new ApiResponse<UserModelDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "برای کاربر باید رمز عبور اختصاصی تعیین شود." };

                IdentityResult result = await _userManager.CreateAsync(user, model.PasswordC);

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
        [ProducesResponseType(typeof(UserModelDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Users.Edit)]
        public async Task<ApiResponse<UserModelDto>> Update([FromBody] UserEditModel model)
        {
            if (string.IsNullOrEmpty(model.UserId)) return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            if (!ModelState.IsValid) return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            try
            {
                var tenantId = User.GetTenantId();
                var getUser = tenantId.HasValue ? await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.UserId && u.TenantId == tenantId.Value) : null;

                if (getUser != null)
                {
                    var mapModel = _map.Map(model, getUser);
                    IdentityResult result = await _userManager.UpdateAsync(mapModel);
                    if (result.Succeeded) return new ApiResponse<UserModelDto>
                    {
                        Status = true,
                        StatusCode = ApiResultStatusCode.Success,
                        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                        Data = ToUserDto(getUser)
                    };
                }
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
            return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.ServerError,
                Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
            };
        }

        [HttpDelete("DeleteUser")]
        [Permission(Permissions.Users.Delete)]
        public async Task<ApiResponse<UserModelDto>> Delete(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            if (!ModelState.IsValid) return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            try
            {

                var tenantId = User.GetTenantId();
                var currentUserId = User.GetUserId();
                var getUser = tenantId.HasValue
                    ? await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId.Value)
                    : null;

                if (getUser == null)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                    };
                }

                // Preserve financial/audit references and invalidate existing JWTs instead of hard-deleting Identity rows.
                if (getUser.IsTenantOwner || getUser.UserType == 4 || getUser.UserType == 110
                    || string.Equals(getUser.Id, currentUserId, StringComparison.Ordinal))
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = "حذف یا غیرفعال‌سازی مدیر مالک، مدیر سیستم یا حساب کاربری جاری مجاز نیست."
                    };
                }

                getUser.UserStatus = false;
                var result = await _userManager.UpdateSecurityStampAsync(getUser);
                if (!result.Succeeded)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.ServerError,
                        Message = string.Join(" | ", result.Errors.Select(x => x.Description))
                    };
                }

                return new ApiResponse<UserModelDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "کاربر با موفقیت غیرفعال شد و نشست‌های قبلی او باطل شدند.",
                    Data = ToUserDto(getUser)
                };
            }
            catch
            {
                return new ApiResponse<UserModelDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
            return new ApiResponse<UserModelDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.ServerError,
                Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
            };
        }

        [HttpPost("AssignRole")]
        [Permission(Permissions.Roles.ManagePermissions)]
        public async Task<ApiResponse> AssignRole(AssignRoleModel model)
        {
            try
            {
                await _roleService.AssignRoleToUserAsync(model.UserId, model.RoleId);

                return new ApiResponse<object> 
                {
                    Status = true,
                    Message = "نقش کاربر با موفقیت ثبت شد.",
                    StatusCode = ApiResultStatusCode.Success
                };
            }
            catch (ArgumentException argEx)
            {
                return new ApiResponse<object>
                {
                    Status = false,
                    Message = $"خطای ورودی: {argEx.Message}",
                    StatusCode = ApiResultStatusCode.BadRequest, 
                    AdditionalData = null
                };
            }
            catch (InvalidOperationException invalidOpEx)
            {
                return new ApiResponse<object>
                {
                    Status = false,
                    Message = $"خطای عملیاتی: {invalidOpEx.Message}",
                    StatusCode = ApiResultStatusCode.NotFound                                 
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<object>
                {
                    Status = false,
                    Message = $"یک خطای غیرمنتظره رخ داد: {ex.Message}",
                    StatusCode = ApiResultStatusCode.ServerError 
                };
            }
        }
        private static UserModelDto ToUserDto(ApplicationUser user) => new()
        {
            Id = user.Id,
            UserFirstName = user.UserFirstName,
            UserLastName = user.UserLastName,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            UserImageUrl = user.UserImageUrl ?? string.Empty,
            UserStatus = user.UserStatus,
            RoleName = string.Empty
        };

    }
}