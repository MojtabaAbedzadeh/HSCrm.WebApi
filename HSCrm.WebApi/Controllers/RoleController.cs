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
                .Where(x => currentTenantId.HasValue &&
                    (
                        (x.TenantId == currentTenantId.Value &&
                         !(x.TenantId == 1 && new[] { "Admin", "Owner", "Customer", "User" }
                             .Contains(x.Name, StringComparer.OrdinalIgnoreCase)))
                        || (x.TenantId == 1 && x.Name == "Owner")
                    ))
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
        public async Task<ApiResponse<UserRole?>> GetUserRole(string userId, CancellationToken ct)
        {
            var currentTenantId = _userService.TenantId;

            var role = await _dbContext.UserRoles
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.User.TenantId == currentTenantId && (x.Role.TenantId == currentTenantId || (x.Role.TenantId == 1 && x.Role.Name == "Owner")))
                .Select(x => new UserRole
                {
                    RoleId = x.RoleId,
                    UserId = x.UserId,
                })
                .FirstOrDefaultAsync(ct);

            return new ApiResponse<UserRole?>
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
                .Where(x => x.Id == roleId && x.TenantId == _userService.TenantId)
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
            if (!currentTenantId.HasValue || currentTenantId.Value <= 0)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شرکت جاری معتبر نیست.",
                    Data = null
                };
            }

            var normalizedName = model.Name.Trim().ToUpperInvariant();
            // ASP.NET Identity has a global unique index on NormalizedName.
            // Check globally before CreateAsync so a cross-tenant collision is reported cleanly.
            var nameExists = await _dbContext.Roles
                .AnyAsync(r => r.NormalizedName == normalizedName);

            if (nameExists)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "این نام نقش قبلاً در سامانه استفاده شده است.",
                    Data = null
                };
            }

            var role = new ApplicationRole
            {
                Id = Guid.NewGuid().ToString(),
                Name = model.Name.Trim(),
                NormalizedName = normalizedName,
                RoleFaName = model.FaName,
                TenantId = currentTenantId.Value
            };

            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded) return new ApiResponse<RoleListModel>
            {
                Status = false,
                StatusCode = result.Errors.Any(e => e.Code == "DuplicateRoleName")
                    ? ApiResultStatusCode.Conflict
                    : ApiResultStatusCode.BadRequest,
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
                    TenantId = currentTenantId.Value
                }
            };
        }

        [HttpPut("UpdateRole")]
        [Permission(Permissions.Roles.Edit)]
        public async Task<ApiResponse<RoleListModel>> UpdateRole([FromBody] RoleEditModel model)
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == model.Id && r.TenantId == _userService.TenantId);

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

            if (IsProtectedSystemRole(role))
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "ویرایش نقش‌های سیستمی مجاز نیست.",
                    Data = null
                };
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "نام نقش الزامی است.",
                    Data = null
                };
            }

            var normalizedName = model.Name.Trim().ToUpperInvariant();
            var nameExists = await _dbContext.Roles
                .AnyAsync(r => r.Id != role.Id && r.NormalizedName == normalizedName);
            if (nameExists)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "این نام نقش قبلاً در سامانه استفاده شده است.",
                    Data = null
                };
            }

            role.Name = model.Name.Trim();
            role.NormalizedName = normalizedName;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = result.Errors.Any(e => e.Code == "DuplicateRoleName")
                        ? ApiResultStatusCode.Conflict
                        : ApiResultStatusCode.BadRequest,
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
                    TenantId = role.TenantId
                }
            };
        }

        [HttpDelete("DeleteRole")]
        [Permission(Permissions.Roles.Delete)]
        public async Task<ApiResponse<RoleListModel>> DeleteRole(string roleId)
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == _userService.TenantId);

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

            if (IsProtectedSystemRole(role))
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "حذف نقش‌های سیستمی مجاز نیست.",
                    Data = null
                };
            }

            var isAssigned = await _dbContext.UserRoles.AnyAsync(x => x.RoleId == roleId);
            if (isAssigned)
            {
                return new ApiResponse<RoleListModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "این نقش به کاربران اختصاص داده شده است؛ ابتدا نقش کاربران را تغییر دهید.",
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
                .Where(x => x.RoleId == roleId && x.Role.TenantId == _userService.TenantId)
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
            var targetRole = await _dbContext.Roles
                .FirstOrDefaultAsync(x => x.Id == model.RoleId && x.TenantId == _userService.TenantId, ct);

            if (targetRole == null)
            {
                return new ApiResponse<bool>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = false
                };
            }

            if (IsProtectedSystemRole(targetRole))
            {
                return new ApiResponse<bool>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "تغییر مجوزهای نقش‌های سیستمی مجاز نیست.",
                    Data = false
                };
            }

            var requestedPermissionIds = (model.PermissionIds ?? new List<int>()).Distinct().ToList();
            var validPermissionCount = await _dbContext.Permissions
                .CountAsync(x => requestedPermissionIds.Contains(x.Id), ct);
            if (validPermissionCount != requestedPermissionIds.Count)
            {
                return new ApiResponse<bool>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "یک یا چند مجوز ارسالی معتبر نیستند.",
                    Data = false
                };
            }

            var oldPermissions = await _dbContext.RolePermissions
                .Where(x => x.RoleId == model.RoleId && x.Role.TenantId == _userService.TenantId)
                .ToListAsync(ct);

            _dbContext.RolePermissions.RemoveRange(oldPermissions);

            var newPermissions = requestedPermissionIds
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

        private static bool IsProtectedSystemRole(ApplicationRole role) =>
            role.TenantId == 1 &&
            new[] { "Admin", "Owner", "Customer", "User" }
                .Contains(role.Name, StringComparer.OrdinalIgnoreCase);
    }
}