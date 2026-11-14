using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IProjectAccessService? _projectAccess;

        public ProjectController(IUnitOfWork context, IProjectAccessService? projectAccess = null)
        {
            _context = context;
            _projectAccess = projectAccess;
        }

        [HttpGet("GetProjects")]
        [Permission(Permissions.Projects.View)]
        public async Task<ApiResponse<IEnumerable<Project>>> Get(CancellationToken ct = default)
        {
            var tenantId = User?.GetTenantId();
            var userId = User?.GetUserId();
            var query = _context.projectUW.Get(
                tenantId.HasValue ? p => p.TenantId == tenantId.Value : null,
                true,
                p => p.Customer);

            if (_projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId))
            {
                if (!await _projectAccess.HasFullProjectAccessAsync(userId, ct))
                {
                    var allowedIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId.Value, userId, ct);
                    query = query.Where(p => allowedIds.Contains(p.Id));
                }
            }

            var projects = query.OrderByDescending(x => x.Id).ToList();
            return new ApiResponse<IEnumerable<Project>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = projects
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Project), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Projects.View)]
        public async Task<ApiResponse> GetById(int projectId, CancellationToken ct = default)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId))
            {
                if (!await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, projectId, ct))
                {
                    if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Forbidden,
                        Message = "شما عضو این پروژه نیستید و مجاز به مشاهده آن نمی‌باشید."
                    };
                }
            }

            var project = _context.projectUW.Get(p => p.Id == projectId && p.TenantId == tenantId.Value).FirstOrDefault();
            if (project == null) return new ApiResponse<Project>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = project
            };
            return new ApiResponse<Project>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = project
            };
        }

        [HttpPost("CreateProject")]
        [Permission(Permissions.Projects.Create)]
        public ApiResponse Create([FromForm] ProjectCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getProject = _context.projectUW.Get(b => b.TenantId == tenantId.Value && b.ProjectTitle == model.ProjectTitle);
            if (getProject.Any()) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };
            try
            {
                Project project = new Project()
                {
                    ProjectTitle = model.ProjectTitle,
                    CustomerId = model.CustomerId,
                    ProjectLocation = model.ProjectLocation,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId(),
                    Status = "Active",
                    TenantId = tenantId.Value
                };
                _context.projectUW.Create(project);
                _context.Save();
                return new ApiResponse<Project>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = project
                };
            }
            catch (Exception)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPut("UpdateProject")]
        [ProducesResponseType(typeof(Project), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Projects.Edit)]
        public ApiResponse Update([FromBody] ProjectEditModel model)
        {
            if (model.ProjectId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };
            var getProject = _context.projectUW.Get(p => p.Id == model.ProjectId && p.TenantId == tenantId.Value, false).FirstOrDefault();
            if (getProject == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
            };

            getProject.ProjectTitle = model.ProjectTitle;
            getProject.ProjectLocation = model.ProjectLocation;
            getProject.CustomerId = model.CustomerId;
            getProject.ModifiedAt = DateTime.UtcNow;
            getProject.ModifiedById = User.GetUserId();

            _context.projectUW.Update(getProject);
            _context.Save();
            return new ApiResponse<Project>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getProject
            };
        }

        [HttpGet("ProjectDropdownList")]
        public async Task<ApiResponse> ProjectDropdownList(int tenantId = 0, CancellationToken ct = default)
        {
            var effectiveTenantId = User.GetTenantId() ?? tenantId;
            var userId = User.GetUserId();
            var query = _context.projectUW.Get(p => p.TenantId == effectiveTenantId);

            if (_projectAccess != null && effectiveTenantId > 0 && !string.IsNullOrWhiteSpace(userId))
            {
                if (!await _projectAccess.HasFullProjectAccessAsync(userId, ct))
                {
                    var allowedIds = await _projectAccess.GetAccessibleProjectIdsAsync(effectiveTenantId, userId, ct);
                    query = query.Where(p => allowedIds.Contains(p.Id));
                }
            }

            IEnumerable<DropdownDto> projectList = query
                .OrderByDescending(p => p.Id)
                .Select(p => new DropdownDto
                {
                    DrId = p.Id,
                    DrName = p.ProjectTitle,
                })
                .ToList();

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = projectList
            };
        }
    }
}
