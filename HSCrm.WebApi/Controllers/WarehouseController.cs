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
    public class WarehouseController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public WarehouseController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetWarehouses")]
        [Permission(Permissions.Warehouses.View)]
        public ApiResponse<IEnumerable<Warehouse>> Get()
        {
            var warehouses = _context.warehouseUW.Get();

            return new ApiResponse<IEnumerable<Warehouse>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = warehouses
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Warehouse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Warehouses.View)]
        public ApiResponse GetById(int warehouseId)
        {
            var warehouse = _context.warehouseUW.GetById(warehouseId);

            if (warehouse == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Warehouse>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = warehouse
            };
        }

        [HttpPost("CreateWarehouse")]
        [Permission(Permissions.Warehouses.Create)]
        public ApiResponse Create([FromForm] WarehouseCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getWarehouse = _context.warehouseUW.Get(c => c.Title == model.Title);

            if (getWarehouse.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Warehouse warehouse = new Warehouse()
                {
                    Title = model.Title,
                    IsActive = model.IsActive,
                    Address = model.Address,
                    CreatedAt = DateTime.Now,
                    CreatedById = User.GetUserId(),
                    TenantId = User.GetTenantId().Value
                };

                _context.warehouseUW.Create(warehouse);
                _context.Save();

                return new ApiResponse<Warehouse>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = warehouse
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

        [HttpPut("UpdateWarehouse")]
        [ProducesResponseType(typeof(Warehouse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Warehouses.Edit)]
        public ApiResponse Update([FromBody] WarehouseEditModel model)
        {
            if (model.WarehouseId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var isDuplicateName = _context.warehouseUW.Get().Any(c => c.Title == model.Title && c.Id != model.WarehouseId && !c.IsDeleted);

            if (isDuplicateName) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getWarehouse = _context.warehouseUW.GetById(model.WarehouseId);

            if (getWarehouse != null)
            {
                getWarehouse.Title = model.Title;
                getWarehouse.IsActive = model.IsActive;
                getWarehouse.Address = model.Address;
                getWarehouse.ModifiedAt = DateTime.Now;
                getWarehouse.ModifiedById = User.GetUserId();
            }

            _context.warehouseUW.Update(getWarehouse);
            _context.Save();

            return new ApiResponse<Warehouse>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getWarehouse
            };
        }

  
        [HttpGet("WarehouseDropdownList")]
        public ApiResponse WarehouseDropdownList()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            var tenantId = User.GetTenantId();

            if (!tenantId.HasValue)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شناسه مستاجر (TenantId) در درخواست موجود نیست."
                };
            }

            var warehouseList = _context.warehouseUW
                .Get(p => p.TenantId == tenantId.Value)
                .Select(p => new DropdownDto
                {
                    DrId = p.Id,
                    DrName = p.Title
                })
                .ToList();

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = warehouseList
            };
        }
    }
}