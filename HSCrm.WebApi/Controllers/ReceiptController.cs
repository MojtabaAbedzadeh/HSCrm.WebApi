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
        private readonly IAccountingService _accountingService;

        public ReceiptController(
            IUnitOfWork context,
            IMapper mapper,
            ICurrentFiscalYearService currentFiscalYearService,
            ICurrentUserService currentUserService,
            IAccountingService accountingService)
        {
            _context = context;
            _mapper = mapper;
            _currentFiscalYearService = currentFiscalYearService;
            _currentUserService = currentUserService;
            _accountingService = accountingService;
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
                x => x.BankAccount!,
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
            var tenantId = _currentUserService.TenantId;
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;
            if (!tenantId.HasValue || !fiscalYearId.HasValue || model.Amount <= 0)
                return BadRequest(new ApiResponse<ReceiptModel> { Status = false, Message = "شرکت، سال مالی یا مبلغ دریافت معتبر نیست." });

            if (!_context.fiscalYearUW.Get(x => x.Id == fiscalYearId.Value && x.TenantId == tenantId.Value && !x.IsClosed && !x.IsDeleted).Any()
                || (model.BankAccountId.HasValue && !_context.bankAccountUW.Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantId.Value && !x.IsDeleted).Any())
                || (model.CustomerId.HasValue && !_context.customerUW.Get(x => x.Id == model.CustomerId.Value && x.TenantId == tenantId.Value && !x.IsDeleted).Any()))
                return BadRequest(new ApiResponse<ReceiptModel> { Status = false, Message = "حساب بانکی، مشتری یا سال مالی متعلق به شرکت جاری نیست." });

            if (model.SalesInvoiceId.HasValue
                && !_context.salesInvoiceUW.Get(x => x.Id == model.SalesInvoiceId.Value && x.TenantId == tenantId.Value && x.FiscalYearId == fiscalYearId.Value && !x.IsDeleted).Any())
                return BadRequest(new ApiResponse<ReceiptModel> { Status = false, Message = "فاکتور فروش متعلق به شرکت/سال مالی جاری نیست." });

            var receipt = _mapper.Map<Receipt>(model);
            receipt.TenantId = tenantId.Value;
            receipt.FiscalYearId = fiscalYearId.Value;
            receipt.CreatedAt = DateTime.UtcNow;
            receipt.CreatedById = _currentUserService.UserId;

            using var transaction = _context.BeginTransaction();
            try
            {
                await _context.receiptUW.CreateAsync(receipt, cancellationToken);
                await _context.SaveAsync();

                await _accountingService.CreateVoucherForReceiptAsync(receipt, cancellationToken);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

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

            var tenantIdForUpdate = _currentUserService.TenantId;
            var fiscalYearForUpdate = _currentFiscalYearService.FiscalYearId;
            if (!tenantIdForUpdate.HasValue || !fiscalYearForUpdate.HasValue || receipt.FiscalYearId != fiscalYearForUpdate.Value || model.Amount <= 0
                || (model.BankAccountId.HasValue && !_context.bankAccountUW.Get(x => x.Id == model.BankAccountId.Value && x.TenantId == tenantIdForUpdate.Value && !x.IsDeleted).Any())
                || (model.CustomerId.HasValue && !_context.customerUW.Get(x => x.Id == model.CustomerId.Value && x.TenantId == tenantIdForUpdate.Value && !x.IsDeleted).Any())
                || (model.SalesInvoiceId.HasValue && !_context.salesInvoiceUW.Get(x => x.Id == model.SalesInvoiceId.Value && x.TenantId == tenantIdForUpdate.Value && x.FiscalYearId == fiscalYearForUpdate.Value && !x.IsDeleted).Any()))
                return BadRequest(new ApiResponse<ReceiptModel> { Status = false, Message = "مشتری، حساب بانکی، فاکتور یا سال مالی معتبر نیست." });

            receipt.SalesInvoiceId = model.SalesInvoiceId;
            receipt.CustomerId = model.CustomerId;
            receipt.BankAccountId = model.BankAccountId;
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
            await _accountingService.CancelVoucherByReferenceAsync(
                HSCrm.Common.PublicTools.Enums.AccountingReferenceType.Receipt,
                receipt.Id,
                "حذف سند دریافت",
                cancellationToken);
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
