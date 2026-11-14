using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectMemberController : ControllerBase
    {
        private readonly IProjectTeamService _teamService;
        private readonly IProjectAccessService _projectAccess;

        public ProjectMemberController(IProjectTeamService teamService, IProjectAccessService projectAccess)
        {
            _teamService = teamService;
            _projectAccess = projectAccess;
        }

        [HttpGet("GetByProject/{projectId:int}")]
        [Permission(Permissions.ProjectMembers.View)]
        public async Task<ActionResult<ApiResponse<List<ProjectMemberDto>>>> GetByProject(int projectId, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<List<ProjectMemberDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            if (!await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, projectId, ct))
                return StatusCode(403, new ApiResponse<List<ProjectMemberDto>> { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به این پروژه دسترسی ندارید." });

            var data = await _teamService.GetProjectMembersAsync(tenantId.Value, projectId, ct);
            return Ok(new ApiResponse<List<ProjectMemberDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست اعضای پروژه با موفقیت دریافت شد.",
                Data = data
            });
        }

        [HttpGet("GetMemberCounts")]
        [Permission(Permissions.Projects.View)]
        public async Task<ActionResult<ApiResponse<Dictionary<int, int>>>> GetMemberCounts(CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<Dictionary<int, int>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var counts = await _teamService.GetProjectMemberCountsAsync(tenantId.Value, userId, ct);
            return Ok(new ApiResponse<Dictionary<int, int>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "تعداد اعضای پروژه‌ها دریافت شد.",
                Data = counts
            });
        }

        [HttpGet("GetAssignableUsers")]
        [Permission(Permissions.ProjectMembers.Manage)]
        public async Task<ActionResult<ApiResponse<List<AssignableUserDto>>>> GetAssignableUsers(CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new ApiResponse<List<AssignableUserDto>> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var users = await _teamService.GetAssignableUsersAsync(tenantId.Value, ct);
            return Ok(new ApiResponse<List<AssignableUserDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "لیست کاربران قابل تخصیص دریافت شد.",
                Data = users
            });
        }

        [HttpPost("AddOrUpdateMember")]
        [Permission(Permissions.ProjectMembers.Manage)]
        public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> AddOrUpdateMember([FromBody] ProjectMemberUpsertModel model, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse<ProjectMemberDto> { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            try
            {
                var result = await _teamService.AddOrUpdateMemberAsync(tenantId.Value, userId, model, ct);
                return Ok(new ApiResponse<ProjectMemberDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "عضویت کاربر در پروژه با موفقیت ثبت شد.",
                    Data = result
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<ProjectMemberDto> { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse<ProjectMemberDto> { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = ex.Message });
            }
        }

        [HttpDelete("RemoveMember/{id:int}")]
        [Permission(Permissions.ProjectMembers.Manage)]
        public async Task<ActionResult<ApiResponse>> RemoveMember(int id, CancellationToken ct)
        {
            var tenantId = User.GetTenantId();
            var userId = User.GetUserId();
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(userId))
                return Unauthorized(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Unauthorized, Message = "دسترسی نامعتبر" });

            var removed = await _teamService.RemoveMemberAsync(tenantId.Value, id, userId, ct);
            if (!removed)
                return NotFound(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.NotFound, Message = "عضویت مورد نظر یافت نشد." });

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "عضو با موفقیت از تیم پروژه حذف شد."
            });
        }
    }
}
