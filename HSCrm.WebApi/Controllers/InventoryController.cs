using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models.Dto;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly ICurrentFiscalYearService _fiscalYearService;
        private readonly IUnitOfWork _context;

        public InventoryController(IInventoryService inventoryService, IUnitOfWork context, ICurrentFiscalYearService fiscalYearService)
        {
            _inventoryService = inventoryService;
            _context = context;
            _fiscalYearService = fiscalYearService;
        }

        [HttpPost("kardex")]
        public async Task<ActionResult<ApiResponse>> GetProductKardex([FromBody] KardexReportFilterDto filter)
        {
            if (filter == null || filter.ProductId <= 0)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "فیلتر گزارش کاردکس معتبر نیست."
                });
            }

            if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate > filter.ToDate)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد."
                });
            }

            var result = await _inventoryService.GetProductKardexAsync(
                filter.ProductId,
                filter.FromDate,
                filter.ToDate,
                filter.WarehouseId);

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "گزارش کاردکس با موفقیت دریافت شد.",
                AdditionalData = result
            });
        }

        [HttpPost("ApplyAdjustment")]
        public async Task<IActionResult> ApplyAdjustment([FromBody] AdjustmentRequest model)
        {
            var tenantId = User.GetTenantId();
            if (tenantId == null)
                return Unauthorized(new ApiResponse
                {
                    Status = false,
                    Message = "شناسه کاربر نامعتبر است."
                });

            var fiscalYearId = _fiscalYearService.FiscalYearId;
            if (fiscalYearId == null)
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    Message = "سال مالی فعال یافت نشد."
                });

            await _inventoryService.ApplyAdjustmentAsync(new AdjustmentDto
            {
                ProductId = model.ProductId,
                WarehouseId = model.WarehouseId,
                QuantityChange = model.ActualQuantity,
                ReferenceId = model.ReferenceId,
                Description = model.Description
            });

            await _context.SaveAsync();

            return Ok(new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "عملیات انبارگردانی با موفقیت در دیتابیس ثبت شد."
            });
        }
    }

    public class AdjustmentRequest
    {
        [Required] public int ProductId { get; set; }
        [Required] public int WarehouseId { get; set; }
        [Required] public decimal ActualQuantity { get; set; }
        public string? Description { get; set; }
        public long ReferenceId { get; set; }
        public InventoryReferenceType ReferenceType { get; set; }
    }
}
