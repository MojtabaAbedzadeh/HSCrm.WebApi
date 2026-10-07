using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Context;
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
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentUserService _userService;

        public RoleController(RoleManager<ApplicationRole> roleManager, ApplicationDbContext dbContext, ICurrentUserService userService)
        {
            _roleManager = roleManager;
            _dbContext = dbContext;
            _userService = userService;
        }

        [HttpGet("GetRoles")]
        [Permission(Permissions.Roles.View)]
        public async Task<ApiResponse<List<RoleListModel>>> GetRoles(CancellationToken ct)
        {
            var currentTenantId = _userService.TenantId;

            var roles = await _dbContext.Roles
                .AsNoTracking()
                // فقط نقش‌های تننت جاری به همراه نقش Owner از سیستم
                .Where(x => x.TenantId == currentTenantId || (x.TenantId == 1 && x.Name == "Owner"))
                .Select(x => new RoleListModel
                {
                    Id = x.Id,
                    Name = x.Name!,
                    FaName = x.RoleFaName,
                    TenantId = x.TenantId
                })
                .ToListAsync(ct);

            return new ApiResponse<List<RoleListModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = roles
            };
        }

        [HttpGet("GetUserRole")]
        [Permission(Permissions.Roles.View)]
        public async Task<ApiResponse<UserRole>> GetUserRole(string userId)
        {
            var currentTenantId = _userService.TenantId;

            var role = _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => new UserRole
                {
                    RoleId = x.RoleId,
                    UserId = x.UserId,
                }).FirstOrDefault();

            return new ApiResponse<UserRole>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = role
            };
        }

        [HttpGet("GetRoleById")]
        [Permission(Permissions.Roles.View)]
        public async Task<ApiResponse<RoleListModel>> GetRoleById(string roleId)
        {
            var role = await _dbContext.Roles
                .AsNoTracking()
                .Where(x => x.Id == roleId)
                .Select(x => new RoleListModel
                {
                    Id = x.Id,
                    Name = x.Name!,
                    TenantId = x.TenantId
                })
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            return new ApiResponse<RoleListModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = role
            };
        }

        [HttpPost("CreateRole")]
        [Permission(Permissions.Roles.Create)]
        public async Task<ApiResponse<RoleListModel>> CreateRole([FromBody] RoleCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse<RoleListModel>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
                Data = null
            };

            var currentTenantId = _userService.TenantId;

            var existsInTenant = await _dbContext.Roles.AnyAsync(r => r.TenantId == currentTenantId && r.Name == model.Name);

            if (existsInTenant)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "این نقش قبلاً در این مجموعه ثبت شده است.",
                    Data = null
                };
            }

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = model.Name,
                NormalizedName = model.Name.ToUpperInvariant(),
                RoleFaName = model.FaName,
                TenantId = User.GetTenantId().Value
            };

            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded) return new ApiResponse<RoleListModel>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.ServerError,
                Message = string.Join(" | ", result.Errors.Select(x => x.Description)),
                Data = null
            };

            return new ApiResponse<RoleListModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new RoleListModel
                {
                    Id = role.Id,
                    Name = role.Name!,
                    TenantId = User.GetTenantId().Value
                }
            };
        }

        [HttpPut("UpdateRole")]
        [Permission(Permissions.Roles.Edit)]
        public async Task<ApiResponse<RoleListModel>> UpdateRole([FromBody] RoleEditModel model)
        {
            var role = await _roleManager.FindByIdAsync(model.Id);

            if (role == null)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            role.Name = model.Name;
            role.NormalizedName = model.Name.ToUpperInvariant();
            role.TenantId = User.GetTenantId().Value;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = string.Join(" | ", result.Errors.Select(x => x.Description)),
                    Data = null
                };
            }

            return new ApiResponse<RoleListModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new RoleListModel
                {
                    Id = role.Id,
                    Name = role.Name!,
                    TenantId = User.GetTenantId().Value
                }
            };
        }

        [HttpDelete("DeleteRole")]
        [Permission(Permissions.Roles.Delete)]
        public async Task<ApiResponse<RoleListModel>> DeleteRole(string roleId)
        {
            var role = await _roleManager.FindByIdAsync(roleId);

            if (role == null)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            var result = await _roleManager.DeleteAsync(role);

            if (!result.Succeeded)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = string.Join(" | ", result.Errors.Select(x => x.Description)),
                    Data = null
                };
            }

            return new ApiResponse<RoleListModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new RoleListModel
                {
                    Id = role.Id,
                    Name = role.Name!,
                    TenantId = User.GetTenantId().Value
                }
            };
        }

        [HttpGet("GetPermissions")]
        [Permission(Permissions.Roles.ManagePermissions)]
        public async Task<ApiResponse<List<PermissionListModel>>> GetPermissions(CancellationToken ct)
        {
            var permissions = await _dbContext.Permissions
                .AsNoTracking()
                .OrderBy(x => x.CategoryTitleEn)
                .ThenBy(x => x.Title)
                .Select(x => new PermissionListModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Title = x.Title,
                    Category = x.CategoryTitleEn
                })
                .ToListAsync(ct);

            return new ApiResponse<List<PermissionListModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = permissions
            };
        }

        [HttpGet("GetRolePermissions")]
        [Permission(Permissions.Roles.ManagePermissions)]
        public async Task<ApiResponse<List<int>>> GetRolePermissions(string roleId, CancellationToken ct)
        {
            var permissionIds = await _dbContext.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .ToListAsync(ct);

            return new ApiResponse<List<int>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = permissionIds
            };
        }

        [HttpPost("UpdateRolePermissions")]
        [Permission(Permissions.Roles.ManagePermissions)]
        public async Task<ApiResponse<bool>> UpdateRolePermissions([FromBody] RolePermissionsModel model, CancellationToken ct)
        {
            var exists = await _dbContext.Roles.AnyAsync(x => x.Id == model.RoleId, ct);

            if (!exists)
            {
                return new ApiResponse<bool>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = false
                };
            }

            var oldPermissions = await _dbContext.RolePermissions
                .Where(x => x.RoleId == model.RoleId)
                .ToListAsync(ct);

            _dbContext.RolePermissions.RemoveRange(oldPermissions);

            var newPermissions = model.PermissionIds
                .Distinct()
                .Select(permissionId => new RolePermission
                {
                    RoleId = model.RoleId,
                    PermissionId = permissionId
                })
                .ToList();

            await _dbContext.RolePermissions.AddRangeAsync(newPermissions, ct);
            await _dbContext.SaveChangesAsync(ct);

            return new ApiResponse<bool>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = true
            };
        }
    }
}