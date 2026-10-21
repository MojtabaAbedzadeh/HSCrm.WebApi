using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FiscalYearController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IAccountingService _accountingService;

        public FiscalYearController(IUnitOfWork context, IAccountingService accountingService)
        {
            _context = context;
            _accountingService = accountingService;
        }

        [HttpGet("GetFiscalYears")]
        public ApiResponse<IEnumerable<FiscalYear>> Get()
        {
            var fiscalYearList = _context.fiscalYearUW.Get().OrderByDescending(f => f.Id);

            return new ApiResponse<IEnumerable<FiscalYear>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = fiscalYearList
            };
        }

        [HttpGet("FiscalYearDropdownList")]
        [AllowAnonymous] // چون قبل از لاگین نهایی فراخوانی می‌شود
        public ApiResponse FiscalYearDropdownList(int tenantId)
        {
            var fiscalYearList = _context.fiscalYearUW.Get(f => f.TenantId == tenantId)
                .OrderByDescending(f => f.Id)
                .Select(f => new DropdownDto
                {
                    DrId = f.Id,
                    DrName = f.Title
                });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = fiscalYearList
            };
        }

        [HttpPost("CloseFiscalYearAndGenerateOpening")]
        [Permission(Permissions.JournalVouchers.Approve)]
        public async Task<ActionResult<ApiResponse<CloseFiscalYearResultDto>>> CloseFiscalYearAndGenerateOpening(
            [FromBody] CloseFiscalYearRequestModel model,
            CancellationToken ct)
        {
            try
            {
                var result = await _accountingService.CloseFiscalYearAndGenerateOpeningAsync(model, ct);
                return Ok(new ApiResponse<CloseFiscalYearResultDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = $"سال مالی {result.ClosedFiscalYearTitle} با موفقیت بسته شد و سند افتتاحیه در سال مالی {result.NewFiscalYearTitle} صادر گردید.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ex.Message
                });
            }
        }
    }
}