using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FiscalYearController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public FiscalYearController(IUnitOfWork context)
        {
            _context = context;
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

        [HttpGet("FiscalYearDropdownList"), AllowAnonymous]
        public ApiResponse FiscalYearDropdownList()
        {
            IEnumerable<DropdownDto> fiscalYearList = _context.fiscalYearUW.Get().OrderByDescending(f => f.Id).Select(f => new DropdownDto
            {
                DrId = f.Id,
                DrName = f.Title
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = fiscalYearList
            };
        }
    }
}
