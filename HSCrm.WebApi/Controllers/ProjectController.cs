using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProjectController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public ProjectController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetProjects")]
        public ApiResponse<IEnumerable<Project>> Get()
        {
            var projects = _context.projectUW.Get();

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
        public ApiResponse GetById(int projectId)
        {
            var projects = _context.projectUW.GetById(projectId);

            if (projects == null) return new ApiResponse<Project>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = projects
            };

            return new ApiResponse<Project>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = projects
            };
        }

        [HttpPost("CreateProject")]
        public ApiResponse Create([FromForm] ProjectCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getProject = _context.projectUW.Get(b => b.ProjectTitle== model.ProjectTitle);

            if (getProject.Count() > 0) return new ApiResponse
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
                    ProjectLocation = model.ProjectLocation,
                    CustomerPhone = model.ProjectOwnerPhone,
                    CreatedAt = DateTime.Now,
                    CreatedById= model.UserId,
                    TenantId = 1
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
        public ApiResponse Update([FromForm] ProjectEditModel model)
        {
            if (model.ProjectId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getProject = _context.projectUW.GetById(model.ProjectId);

            if (getProject != null)
            {
                getProject.ProjectTitle = model.ProjectTitle;
                getProject.ProjectLocation = model.ProjectLocation;
                getProject.CustomerPhone = model.ProjectOwnerPhone;
            }

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
        public ApiResponse ProjectDropdownList()
        {
            IEnumerable<DropdownDto> projectList = _context.projectUW.Get().Select(p => new DropdownDto
            {
                DrId = p.Id,
                DrName = p.ProjectTitle,
            });

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
