using Microsoft.AspNetCore.Authorization;
﻿using AutoMapper;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
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
        [HttpGet("GetReceipts")]
        [Permission(Permissions.Receipt.View)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ReceiptModel>>>> GetAll(CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId ?? User.GetTenantId();
            var fiscalYearId = _currentFiscalYearService.FiscalYearId;

            var receipts = await _context.receiptUW.GetAsync(
                x => (!tenantId.HasValue || x.TenantId == tenantId.Value)
                     && (!fiscalYearId.HasValue || x.FiscalYearId == fiscalYearId.Value),
                true,
                cancellationToken,
                x => x.BankAccount!,
                x => x.SalesInvoice!,
                x => x.SalesInvoice!.Project.Customer,
                x => x.Customer!);

            var orderedReceipts = receipts
                .OrderByDescending(x => x.ReceiptDate)
                .ThenByDescending(x => x.Id)
                .ToList();

            var result = _mapper.Map<IEnumerable<ReceiptModel>>(orderedReceipts);

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
        [HttpPost("Create")]
        [Permission(Permissions.Receipt.Create)]
        public async Task<ActionResult<ApiResponse<ReceiptModel>>> Create([FromBody] ReceiptCreateModel model, CancellationToken cancellationToken)
        {
            if (model == null || model.Amount <= 0)
            {
                return BadRequest(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "مبلغ دریافتی باید بزرگ‌تر از صفر باشد."
                });
            }

            var tenantId = _currentUserService.TenantId ?? User.GetTenantId() ?? (model.TenantId > 0 ? model.TenantId : (int?)null);
            var fiscalYearId = _currentFiscalYearService.FiscalYearId ?? 0;

            if (!tenantId.HasValue || tenantId.Value <= 0 || fiscalYearId <= 0)
            {
                return BadRequest(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "اطلاعات شرکت یا سال مالی فعال معتبر نیست."
                });
            }

            var tenant = await _context.tenantUW.GetByIdAsync(tenantId.Value, cancellationToken);
            if (tenant?.DocumentLockDate != null && model.ReceiptDate.Date <= tenant.DocumentLockDate.Value.Date)
            {
                return BadRequest(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "ثبت دریافت در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                });
            }

            var receipt = _mapper.Map<Receipt>(model);
            receipt.Tenant = null!;
            receipt.TenantId = tenantId.Value;
            receipt.FiscalYearId = fiscalYearId;
            receipt.CreatedAt = DateTime.UtcNow;
            receipt.CreatedById = User.GetUserId() ?? _currentUserService.UserId ?? model.UserId;

            if (receipt.SalesInvoiceId.HasValue && !receipt.CustomerId.HasValue)
            {
                var salesInvoice = _context.salesInvoiceUW
                    .Get(x => x.Id == receipt.SalesInvoiceId.Value, true, x => x.Project)
                    .FirstOrDefault();
                if (salesInvoice?.Project?.CustomerId != null)
                {
                    receipt.CustomerId = salesInvoice.Project.CustomerId;
                }
            }

            using var transaction = _context.BeginTransaction();
            try
            {
                await _context.receiptUW.CreateAsync(receipt, cancellationToken);
                await _context.SaveAsync();

                await _accountingService.CreateVoucherForReceiptAsync(receipt, cancellationToken);

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return BadRequest(new ApiResponse<ReceiptModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "خطا در ثبت دریافت: " + ex.Message
                });
            }

            var result = _mapper.Map<ReceiptModel>(receipt);

            return Ok(new ApiResponse<ReceiptModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = result,
                Message = "دریافت با موفقیت ثبت شد و سند حسابداری صادر گردید."
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
            receipt.BankAccountId = model.BankAccountId;
            receipt.ReceiptDate = model.ReceiptDate;
            receipt.Amount = model.Amount;
            receipt.Method = model.Method;
            receipt.ReferenceNo = model.ReferenceNo;
            receipt.Description = model.Description;
            receipt.ModifiedAt = DateTime.UtcNow;
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
