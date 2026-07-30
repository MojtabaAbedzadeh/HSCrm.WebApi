using AutoMapper;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IMapper _mapper;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly ICurrentUserService _currentUserService;

        public ReceiptController(
            IUnitOfWork context,
            IMapper mapper,
            ICurrentFiscalYearService currentFiscalYearService,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _mapper = mapper;
            _currentFiscalYearService = currentFiscalYearService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        [Permission(Permissions.Receipt.View)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ReceiptModel>>>> GetAll(CancellationToken cancellationToken)
        {
            var receipts = await _context.receiptUW.GetAsync(
                x => x.TenantId == _currentUserService.TenantId
                     && x.FiscalYearId == _currentFiscalYearService.FiscalYearId,
                true,
                cancellationToken,
                x => x.SalesInvoice!,
                x => x.Customer!);

            var result = _mapper.Map<IEnumerable<ReceiptModel>>(receipts);

            return Ok(new ApiResponse<IEnumerable<ReceiptModel>>
            {
                Status = true,
                Data = result
            });
        }

        [HttpGet("{id:int}")]
        [Permission(Permissions.Receipt.View)]
        public async Task<ActionResult<ApiResponse<ReceiptModel>>> GetById(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.receiptUW.GetByIdAsync(id, cancellationToken);

            if (receipt == null || receipt.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    Message = "Receipt not found"
                });
            }

            var result = _mapper.Map<ReceiptModel>(receipt);

            return Ok(new ApiResponse<ReceiptModel>
            {
                Status = true,
                Data = result
            });
        }

        [HttpPost]
        [Permission(Permissions.Receipt.Create)]
        public async Task<ActionResult<ApiResponse<ReceiptModel>>> Create([FromBody] ReceiptCreateModel model, CancellationToken cancellationToken)
        {
            var receipt = _mapper.Map<Receipt>(model);

            receipt.TenantId = _currentUserService.TenantId ?? model.TenantId;
            receipt.FiscalYearId = (int)_currentFiscalYearService.FiscalYearId;
            receipt.CreatedAt = DateTime.Now;
            receipt.CreatedById = model.UserId;

            await _context.receiptUW.CreateAsync(receipt, cancellationToken);
            await _context.SaveAsync();

            var result = _mapper.Map<ReceiptModel>(receipt);

            return Ok(new ApiResponse<ReceiptModel>
            {
                Status = true,
                Data = result,
                Message = "Receipt created successfully"
            });
        }

        [HttpPut("{id:int}")]
        [Permission(Permissions.Receipt.Edit)]
        public async Task<ActionResult<ApiResponse<ReceiptModel>>> Update(int id, [FromBody] ReceiptEditModel model, CancellationToken cancellationToken)
        {
            var receipt = await _context.receiptUW.GetByIdAsync(id, cancellationToken);

            if (receipt == null || receipt.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    Message = "Receipt not found"
                });
            }

            receipt.SalesInvoiceId = model.SalesInvoiceId;
            receipt.CustomerId = model.CustomerId;
            receipt.ReceiptDate = model.ReceiptDate;
            receipt.Amount = model.Amount;
            receipt.Method = model.Method;
            receipt.ReferenceNo = model.ReferenceNo;
            receipt.Description = model.Description;
            receipt.ModifiedAt = DateTime.Now;
            receipt.ModifiedById = _currentUserService.UserId;

            _context.receiptUW.Update(receipt, cancellationToken);
            await _context.SaveAsync();

            var result = _mapper.Map<ReceiptModel>(receipt);

            return Ok(new ApiResponse<ReceiptModel>
            {
                Status = true,
                Data = result,
                Message = "Receipt updated successfully"
            });
        }

        [HttpDelete("{id:int}")]
        [Permission(Permissions.Receipt.Delete)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id, CancellationToken cancellationToken)
        {
            var receipt = await _context.receiptUW.GetByIdAsync(id, cancellationToken);

            if (receipt == null || receipt.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<bool>
                {
                    Status = false,
                    Message = "Receipt not found"
                });
            }

            _context.receiptUW.Delete(receipt, cancellationToken);
            await _context.SaveAsync();

            return Ok(new ApiResponse<bool>
            {
                Status = true,
                Data = true,
                Message = "Receipt deleted successfully"
            });
        }
    }
}
