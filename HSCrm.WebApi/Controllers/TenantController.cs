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

        [HttpGet("GetBusiness")]
        public ApiResponse<IEnumerable<Tenant>> Get()
        {
            var branches = _context.businessUW.Get();

            return new ApiResponse<IEnumerable<Tenant>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = branches
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(int businessId)
        {
            var business = _context.businessUW.GetById(businessId);

            if (business == null) return new ApiResponse<Tenant>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = business
            };

            return new ApiResponse<Tenant>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = business
            };
        }

        [HttpPost("CreateBusiness")]
        public ApiResponse Create([FromForm] BusinessCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getBusiness = _context.businessUW.Get(b => b.TenantTitle == model.BusinessTitle);

            if (getBusiness.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Tenant business = new Tenant()
                {
                    TenantTitle = model.BusinessTitle,
                    TenantOwner = model.BusinessOwner,
                    TenantOwnerMobile = model.BusinessOwnerMobile,
                    CreatedAt = DateTime.Now,
                    CreatedById = model.BusinessOwner,

                };

                _context.businessUW.Create(business);
                _context.Save();

                return new ApiResponse<Tenant>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = business
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

        [HttpPut("UpdateBussiness")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse Update([FromForm] BusinessEditModel model)
        {
            if (model.BusinessId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getBusiness = _context.businessUW.GetById(model.BusinessId);

            if (getBusiness != null)
            {
                getBusiness.TenantTitle = model.BusinessTitle;
                getBusiness.TenantOwner = model.BusinessOwner;
                getBusiness.TenantOwnerMobile = model.BusinessOwnerMobile;
            }

            _context.businessUW.Update(getBusiness);
            _context.Save();

            return new ApiResponse<Tenant>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getBusiness
            };
        }

        [HttpGet("BusinessDropdownList")]
        public ApiResponse BusinessDropdownList()
        {
            IEnumerable<DropdownDto> businessList = _context.businessUW.Get().Select(w => new DropdownDto
            {
                DrId = w.Id,
                DrName = w.TenantTitle,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = businessList
            };
        }
    }
}
