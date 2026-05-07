using HSCrm.Common.PublicTools;
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
    public class TenantController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public TenantController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetTenant")]
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
                Tenant tenant = new Tenant()
                {
                    TenantTitle = model.TenantTitle,
                    TenantOwner = model.TenantOwner,
                    TenantOwnerMobile = model.TenantOwnerMobile,
                    CreatedAt = DateTime.Now,
                    CreatedById = model.TenantOwner,
                    Currency = model.Currency                };

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
        public ApiResponse Update([FromForm] TenantEditModel model)
        {
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
                getTenant.TenantOwner = model.TenantOwner;
                getTenant.TenantOwnerMobile = model.TenantOwnerMobile;
                getTenant.ModifiedAt = DateTime.Now;
                getTenant.ModifiedById = model.TenantOwner;
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
