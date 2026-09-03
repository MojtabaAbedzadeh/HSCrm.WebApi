using Common.Permissions;
using Framework.Authorization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public TenantController(IUnitOfWork context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpGet("GetTenant")]
        [Permission(Permissions.Tenants.View)]
        public ApiResponse<IEnumerable<Tenant>> Get()
        {
            var tenants = _context.tenantUW.Get();

            return new ApiResponse<IEnumerable<Tenant>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenants
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Tenants.View)]
        public ApiResponse GetById(int tenantId)
        {
            var tenants = _context.tenantUW.GetById(tenantId);

            if (tenants == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Tenant>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenants
            };
        }

        [HttpPost("CreateTenant")]
        [Permission(Permissions.Tenants.Create)]
        public ApiResponse Create([FromForm] TenantCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getBusiness = _context.tenantUW.Get(b => b.TenantTitle == model.TenantTitle);

            if (getBusiness.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                var userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                Tenant tenant = new Tenant()
                {
                    TenantTitle = model.TenantTitle,
                    CompanyPhone = model.CompanyPhone,
                    Address = model.Address,
                    CompanyEmail = model.CompanyEmail,
                    IsActive = model.IsActive,
                    IsDeleted = false,
                    LogoUrl = model.LogoUrl,
                    TenantCode = TenantCodeGenerator.Generate(model.TenantTitle),
                    CreatedAt = DateTime.Now,
                    CreatedById = User.GetUserId(),
                    Currency = model.Currency
                };

                _context.tenantUW.Create(tenant);
                _context.Save();

                return new ApiResponse<Tenant>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = tenant
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

        [HttpPut("UpdateTenant")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Tenants.Edit)]
        public ApiResponse Update([FromForm] TenantEditModel model)
        {
            var userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (model.TenantId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getTenant = _context.tenantUW.GetById(model.TenantId);

            if (getTenant != null)
            {
                getTenant.TenantTitle = model.TenantTitle;
                getTenant.Address = model.Address;
                getTenant.ModifiedAt = DateTime.Now;
                getTenant.ModifiedById = User.GetUserId();
            }

            _context.tenantUW.Update(getTenant);
            _context.Save();

            return new ApiResponse<Tenant>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getTenant
            };
        }

        [HttpGet("TenantDropdownList")]
        public ApiResponse TenantDropdownList()
        {
            IEnumerable<DropdownDto> tenantList = _context.tenantUW.Get().Select(w => new DropdownDto
            {
                DrId = w.Id,
                DrName = w.TenantTitle,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenantList
            };
        }
    }
}
