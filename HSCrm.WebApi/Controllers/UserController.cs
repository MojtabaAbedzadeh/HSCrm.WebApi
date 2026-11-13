using Microsoft.AspNetCore.Authorization;
﻿using AutoMapper;
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
    [Authorize]
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
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
            {
                return new ApiResponse<List<ApplicationUser>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var persons = (await _context.userManagerUW
                .GetAsync(u => u.UserType != 3 && u.UserType != 110 && u.TenantId == tenantId.Value, asNoTracking: true, ct: ct))
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
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };
            var request = _context.userManagerUW.Get(u => u.Id == userId && u.TenantId == tenantId.Value).FirstOrDefault();

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

            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
            {
                return new ApiResponse<UserModelDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            try
            {
                // بررسی تکراری بودن
                var userNameExist = await _userManager.FindByNameAsync(model.UserNameC.Trim());
                var emailExist = !string.IsNullOrWhiteSpace(model.EmailC)
                    ? await _userManager.FindByEmailAsync(model.EmailC.Trim())
                    : null;
                var phoneExist = _userManager.Users.FirstOrDefault(x => x.PhoneNumber == model.PhoneNumberC.Trim());

                if (userNameExist != null || emailExist != null || phoneExist != null)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = "نام کاربری، ایمیل یا شماره موبایل تکراری است."
                    };
                }

                ApplicationUser user = new ApplicationUser()
                {
                    UserFirstName = model.UserFirstNameC.Trim(),
                    UserLastName = model.UserLastNameC.Trim(),
                    UserImageUrl = model.UserImageUrlC,
                    UserStatus = true,
                    UserType = 0,
                    UserGender = model.UserGenderC,
                    PhoneNumber = model.PhoneNumberC.Trim(),
                    Email = string.IsNullOrWhiteSpace(model.EmailC) ? null : model.EmailC.Trim(),
                    TenantId = tenantId.Value,
                    IsTenantOwner = false,
                    UserName = model.UserNameC.Trim()
                };

                var password = !string.IsNullOrWhiteSpace(model.Password) ? model.Password : "@Omid1364";
                IdentityResult result = await _userManager.CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    return new ApiResponse<UserModelDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = string.Join(" | ", result.Errors.Select(x => x.Description))
                    };
                }

                // افزودن نقش
                string roleName = user.UserType == 1 ? "Admin" : "user";
                await _userManager.AddToRoleAsync(user, roleName);

                var dto = new UserModelDto
                {
                    Id = user.Id,
                    UserFirstName = user.UserFirstName,
                    UserLastName = user.UserLastName,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber ?? string.Empty,
                    UserImageUrl = user.UserImageUrl ?? string.Empty,
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
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name),
            };

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
                if (getUser == null || getUser.TenantId != tenantId.Value)
                {
                    return new ApiResponse<ApplicationUser>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    };
                }

                var mapModel = _map.Map(model, getUser);
                IdentityResult result = await _userManager.UpdateAsync(mapModel);
                if (result.Succeeded) return new ApiResponse<ApplicationUser>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = getUser
                };

                return new ApiResponse<ApplicationUser>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = string.Join(" | ", result.Errors.Select(e => e.Description)),
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

        [HttpDelete("DeleteUser")]
        [Permission(Permissions.Users.Delete)]
        public async Task<ApiResponse<ApplicationUser>> Delete(string userId)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name),
            };

            if (string.IsNullOrEmpty(userId)) return new ApiResponse<ApplicationUser>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            try
            {
                var getUser = await _userManager.FindByIdAsync(userId);
                if (getUser == null || getUser.TenantId != tenantId.Value)
                {
                    return new ApiResponse<ApplicationUser>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    };
                }

                _context.userManagerUW.Delete(getUser);
                await _context.SaveAsync();
                return new ApiResponse<ApplicationUser>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = getUser
                };
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
        }

        [HttpPost("AssignRole")]
        [Permission(Permissions.Users.Create)]
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
    }
}