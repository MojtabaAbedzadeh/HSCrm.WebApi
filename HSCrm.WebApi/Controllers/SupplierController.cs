using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupplierController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SupplierController(IUnitOfWork context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("GetSupplier")]
        public ApiResponse<IEnumerable<Supplier>> Get()
        {
            var suppliers = _context.supplierUW.Get();

            return new ApiResponse<IEnumerable<Supplier>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = suppliers
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Supplier), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(int supplierId)
        {
            var supplier = _context.supplierUW.GetById(supplierId);

            if (supplier == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Supplier>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = supplier
            };
        }

        [HttpPost("CreateSupplier")]
        public ApiResponse Create([FromForm] SupplierCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getSupplier = _context.supplierUW.Get(b => b.SupplierFullName == model.SupplierFullName);

            if (getSupplier.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Supplier supplier = new Supplier()
                {
                    SupplierFullName = model.SupplierFullName,
                    SupplierAddress = model.SupplierAddress,
                    SupplierPhone = model.SupplierPhone,
                    CreatedById = model.CreateById,
                    TenantId = model.TenantId,
                    CreatedAt = DateTime.Now,
                };

                _context.supplierUW.Create(supplier);
                _context.Save();

                return new ApiResponse<Supplier>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = supplier
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

        [HttpPut("UpdateSupplier")]
        [ProducesResponseType(typeof(Supplier), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse Update([FromBody] SupplierEditModel model)
        {
            if (model.SupplierId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getSupplier = _context.supplierUW.GetById(model.SupplierId);

            if (getSupplier != null)
            {
                getSupplier.SupplierFullName = model.SupplierFullName;
                getSupplier.SupplierPhone = model.SupplierPhone;
                getSupplier.SupplierAddress = model.SupplierAddress;
                getSupplier.ModifiedAt = DateTime.Now;
                getSupplier.ModifiedById = model.ModifiedById;
            }

            _context.supplierUW.Update(getSupplier);
            _context.Save();

            return new ApiResponse<Supplier>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getSupplier
            };
        }

        [HttpGet("SupplierDropdownList")]
        public ApiResponse SupplierDropdownList(int tenantId)
        {
            IEnumerable<DropdownDto> supplierList = _context.supplierUW.Get(s => s.TenantId == tenantId).Select(p => new DropdownDto
            {
                DrId = p.Id,
                DrName = p.SupplierFullName,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = supplierList
            };
        }
    }
}
