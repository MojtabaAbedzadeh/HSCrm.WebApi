using Microsoft.AspNetCore.Authorization;
﻿using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseReturnInvoiceController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IInventoryService _inventory;
        private readonly IAccountingService _accountingService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly ILogger<PurchaseReturnInvoiceController> _logger;
        private readonly IInvoiceNumberGenerator? _invoiceNumberGenerator;

        public PurchaseReturnInvoiceController(
            IUnitOfWork context,
            IInventoryService inventory,
            IAccountingService accountingService,
            ICurrentFiscalYearService currentFiscalYearService,
            ILogger<PurchaseReturnInvoiceController> logger,
            IInvoiceNumberGenerator? invoiceNumberGenerator = null)
        {
            _context = context;
            _inventory = inventory;
            _accountingService = accountingService;
            _currentFiscalYearService = currentFiscalYearService;
            _logger = logger;
            _invoiceNumberGenerator = invoiceNumberGenerator;
        }

        [HttpGet("GetNextInvoiceNumber")]
        public async Task<ApiResponse<object>> GetNextInvoiceNumber([FromQuery] int? fiscalYearId = null)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<object>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var fyId = fiscalYearId ?? _currentFiscalYearService?.FiscalYearId;
            var tenant = _context.tenantUW?.GetById(tenantId.Value);
            var mode = string.IsNullOrWhiteSpace(tenant?.InvoiceNumberingMode) ? "Hybrid" : tenant.InvoiceNumberingMode;

            var nextNumber = string.Equals(mode, "Manual", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : (_invoiceNumberGenerator != null
                    ? await _invoiceNumberGenerator.GenerateNextPurchaseReturnNumberAsync(tenantId.Value, fyId)
                    : $"PRI-{DateTime.UtcNow.Year}-0001");

            return new ApiResponse<object>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new
                {
                    nextInvoiceNumber = nextNumber,
                    pattern = tenant?.PurchaseReturnPattern ?? "PRI-{YEAR}-{SEQ:4}",
                    numberingMode = mode,
                    defaultVatPercent = tenant?.DefaultVatPercent ?? 0m
                }
            };
        }

        [HttpGet("GetPurchaseReturnInvoices")]
        [HttpGet("GetAll")]
        [Permission(Permissions.PurchaseReturnInvoices.View)]
        public ApiResponse<IEnumerable<PurchaseReturnInvoiceModel>> Get(
            string? supplierFullName = null,
            int? purchaseInvoiceId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _context.purchaseReturnInvoiceUW
                .Query()
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.PurchaseInvoice)
                .Include(x => x.Warehouse)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(supplierFullName))
            {
                query = query.Where(x => x.Supplier != null && x.Supplier.SupplierFullName.Contains(supplierFullName));
            }

            if (purchaseInvoiceId.HasValue)
            {
                query = query.Where(x => x.PurchaseInvoiceId == purchaseInvoiceId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.IssueDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(x => x.IssueDate < endDateExclusive);
            }

            var list = query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToList();

            var result = list.Select(x => new PurchaseReturnInvoiceModel
            {
                Id = x.Id,
                Number = x.Number,
                PurchaseInvoiceId = x.PurchaseInvoiceId,
                OriginalPurchaseInvoiceNumber = x.PurchaseInvoice?.Number,
                SupplierId = x.SupplierId,
                SupplierFullName = x.Supplier?.SupplierFullName,
                WarehouseId = x.WarehouseId,
                WarehouseTitle = x.Warehouse?.Title,
                IssueDate = x.IssueDate,
                Status = x.Status,
                Subtotal = x.Subtotal,
                Tax = x.Tax,
                Discount = x.Discount,
                Total = x.Total,
                Description = x.Description
            }).ToList();

            return new ApiResponse<IEnumerable<PurchaseReturnInvoiceModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = result
            };
        }

        [HttpGet("{id}")]
        [Permission(Permissions.PurchaseReturnInvoices.View)]
        public ApiResponse GetById(int id)
        {
            var returnInvoice = _context.purchaseReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                    .ThenInclude(i => i.Product)
                .Include(x => x.PurchaseInvoice)
                .Include(x => x.Supplier)
                .Include(x => x.Warehouse)
                .FirstOrDefault(x => x.Id == id);

            if (returnInvoice == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "فاکتور برگشت از خرید یافت نشد.",
                    StatusCode = ApiResultStatusCode.NotFound
                };
            }

            if (returnInvoice.Items != null)
            {
                foreach (var item in returnInvoice.Items)
                {
                    item.PurchaseReturnInvoice = null!;
                }
            }

            return new ApiResponse<PurchaseReturnInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = returnInvoice
            };
        }

        [HttpPost("CreatePurchaseReturnInvoice")]
        [HttpPost("Create")]
        [Permission(Permissions.PurchaseReturnInvoices.Create)]
        public async Task<IActionResult> Create(
            [FromBody] PurchaseReturnInvoiceCreateModel model,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Items == null || !model.Items.Any())
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "فاکتور برگشت از خرید باید حداقل یک ردیف کالا داشته باشد."
                });

            var tenantId = User.GetTenantId() ?? (model.TenantId > 0 ? model.TenantId : (int?)null);
            if (!tenantId.HasValue)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شناسه شرکت معتبر نیست."
                });
            }

            var fiscalYearId = _currentFiscalYearService.FiscalYearId;
            if (fiscalYearId == null)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "هیچ سال مالی فعالی برای سیستم تعریف نشده است."
                });
            }

            DateTime issueDate;
            try
            {
                issueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate);
            }
            catch
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "تاریخ صدور معتبر نیست."
                });
            }

            var tenant = _context.tenantUW?.GetById(tenantId.Value);
            if (tenant?.DocumentLockDate != null && issueDate.Date <= tenant.DocumentLockDate.Value.Date)
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "ثبت برگشت از خرید در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                });
            }

            var numberingMode = tenant?.InvoiceNumberingMode ?? "Hybrid";
            if (string.Equals(numberingMode, "Manual", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "در حالت شماره‌گذاری دستی، وارد کردن شماره فاکتور الزامی است."
                });
            }

            if (string.Equals(numberingMode, "Auto", StringComparison.OrdinalIgnoreCase) && _invoiceNumberGenerator != null)
            {
                model.InvoiceNumber = await _invoiceNumberGenerator.GenerateNextPurchaseReturnNumberAsync(tenantId.Value, fiscalYearId, issueDate);
            }
            else if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                model.InvoiceNumber = _invoiceNumberGenerator != null
                    ? await _invoiceNumberGenerator.GenerateNextPurchaseReturnNumberAsync(tenantId.Value, fiscalYearId, issueDate)
                    : $"PRI-{issueDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
            }

            var resolvedNumber = model.InvoiceNumber!.CleanName();
            var purchaserUserId = !string.IsNullOrWhiteSpace(model.PurchaserUserId) ? model.PurchaserUserId : (User.GetUserId() ?? string.Empty);

            // اگر فاکتور خرید مرجع انتخاب شده، کنترل عدم برگشت بیش از تعداد خریداری‌شده
            if (model.PurchaseInvoiceId.HasValue && model.PurchaseInvoiceId.Value > 0)
            {
                var origPurchase = await _context.purchaseInvoiceUW
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.Id == model.PurchaseInvoiceId.Value, ct);

                if (origPurchase == null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = "فاکتور خرید مرجع یافت نشد."
                    });
                }

                var previousReturns = await _context.purchaseReturnInvoiceUW
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x => x.PurchaseInvoiceId == model.PurchaseInvoiceId.Value && x.Status == InvoiceStatus.Final && !x.IsDeleted)
                    .ToListAsync(ct);

                foreach (var reqItem in model.Items)
                {
                    var purchasedQty = origPurchase.Items
                        .Where(x => x.ProductId == reqItem.ProductId)
                        .Sum(x => x.Quantity);

                    var alreadyReturnedQty = previousReturns
                        .SelectMany(r => r.Items)
                        .Where(i => i.ProductId == reqItem.ProductId)
                        .Sum(i => i.Quantity);

                    if (reqItem.Quantity + alreadyReturnedQty > purchasedQty)
                    {
                        return BadRequest(new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = $"تعداد برگشتی کالا (شناسه {reqItem.ProductId}) بیشتر از تعداد قابل برگشت فاکتور خرید مرجع است. (خریداری‌شده: {purchasedQty}، برگشت قبلی: {alreadyReturnedQty})"
                        });
                    }
                }
            }

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                var returnInvoice = new PurchaseReturnInvoice
                {
                    Number = resolvedNumber,
                    PurchaseInvoiceId = model.PurchaseInvoiceId,
                    SupplierId = model.SupplierId,
                    WarehouseId = model.WarehouseId,
                    TenantId = tenantId.Value,
                    FiscalYearId = fiscalYearId.Value,
                    IssueDate = issueDate,
                    PurchaserUserId = purchaserUserId,
                    Status = model.Status,
                    CreatedById = purchaserUserId,
                    CreatedAt = DateTime.UtcNow,
                    Description = model.Description ?? $"فاکتور برگشت از خرید شماره {resolvedNumber}",
                    Items = new List<PurchaseReturnInvoiceItem>()
                };

                decimal subTotal = 0m;
                decimal totalItemsTax = 0m;
                decimal totalItemsDiscount = 0m;

                foreach (var itemDto in model.Items)
                {
                    if (itemDto.Quantity <= 0 || itemDto.UnitPrice < 0)
                        throw new InvalidOperationException("مقادیر تعداد یا قیمت واحد برگشتی نامعتبر است.");

                    var lineSubTotal = itemDto.Quantity * itemDto.UnitPrice;
                    var lineTotal = lineSubTotal - itemDto.Discount + itemDto.Tax;

                    returnInvoice.Items.Add(new PurchaseReturnInvoiceItem
                    {
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        UnitPriceReturn = itemDto.UnitPrice,
                        Discount = itemDto.Discount,
                        Tax = itemDto.Tax,
                        LineTotal = lineTotal,
                        TenantId = tenantId.Value,
                        CreatedById = purchaserUserId,
                        CreatedAt = DateTime.UtcNow
                    });

                    subTotal += lineSubTotal;
                    totalItemsDiscount += itemDto.Discount;
                    totalItemsTax += itemDto.Tax;
                }

                returnInvoice.Subtotal = subTotal;
                returnInvoice.Discount = totalItemsDiscount + model.InvoiceDiscount;
                returnInvoice.Tax = totalItemsTax + model.InvoiceTax;
                returnInvoice.Total = returnInvoice.Subtotal - returnInvoice.Discount + returnInvoice.Tax;

                await _context.purchaseReturnInvoiceUW.CreateAsync(returnInvoice, ct);
                await _context.SaveAsync();

                if (returnInvoice.Status == InvoiceStatus.Final)
                {
                    await _inventory.ApplyInventoryForPurchaseReturnAsync(returnInvoice, ct);
                    await _context.SaveAsync();

                    await _accountingService.CreateVoucherForPurchaseReturnAsync(returnInvoice, ct);
                }

                await transaction.CommitAsync(ct);

                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از خرید با موفقیت ثبت شد و آثار انبار و حسابداری اعمال گردید."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error in PurchaseReturnInvoiceController.Create");
                return StatusCode(500, new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("Confirm/{id}")]
        [HttpPost("ConfirmInvoice/{id}")]
        [HttpPost("{id:int}/Confirm")]
        [Permission(Permissions.PurchaseReturnInvoices.Approve)]
        public async Task<IActionResult> Confirm(int id, CancellationToken ct = default)
        {
            var returnInvoice = await _context.purchaseReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (returnInvoice == null)
                return NotFound("فاکتور برگشت از خرید یافت نشد.");

            var tenant = _context.tenantUW?.GetById(returnInvoice.TenantId);
            if (tenant?.DocumentLockDate != null && returnInvoice.IssueDate.Date <= tenant.DocumentLockDate.Value.Date)
                return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "تأیید فاکتور در تاریخ‌های قفل‌شده امکان‌پذیر نیست." });

            if (returnInvoice.Status != InvoiceStatus.Proforma)
                return BadRequest("فقط پیش‌فاکتور برگشتی قابل تأیید نهایی است.");

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                returnInvoice.Status = InvoiceStatus.Final;
                returnInvoice.ModifiedAt = DateTime.UtcNow;

                await _inventory.ApplyInventoryForPurchaseReturnAsync(returnInvoice, ct);
                _context.purchaseReturnInvoiceUW.Update(returnInvoice);
                await _context.SaveAsync();

                await _accountingService.CreateVoucherForPurchaseReturnAsync(returnInvoice, ct);

                await transaction.CommitAsync(ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از خرید نهایی شد و سند انبار و حسابداری صادر گردید."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("Cancel/{id}")]
        [HttpPost("CancelInvoice/{id}")]
        [HttpPost("{id:int}/Cancel")]
        [Permission(Permissions.PurchaseReturnInvoices.Cancel)]
        public async Task<IActionResult> Cancel(int id, [FromBody] string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("ذکر دلیل ابطال الزامی است.");

            var returnInvoice = await _context.purchaseReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (returnInvoice == null)
                return NotFound("فاکتور برگشت از خرید یافت نشد.");

            var tenant = _context.tenantUW?.GetById(returnInvoice.TenantId);
            if (tenant?.DocumentLockDate != null && returnInvoice.IssueDate.Date <= tenant.DocumentLockDate.Value.Date)
                return BadRequest(new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.BadRequest, Message = "ابطال فاکتور در تاریخ‌های قفل‌شده امکان‌پذیر نیست." });

            if (returnInvoice.Status == InvoiceStatus.Cancelled)
                return BadRequest("این فاکتور برگشتی قبلاً باطل شده است.");

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (returnInvoice.Status == InvoiceStatus.Final)
                {
                    _inventory.RollbackInventoryForPurchaseReturn(returnInvoice);
                    await _accountingService.CancelVoucherByReferenceAsync(
                        AccountingReferenceType.PurchaseReturnInvoice,
                        returnInvoice.Id,
                        reason,
                        ct);
                }

                returnInvoice.Status = InvoiceStatus.Cancelled;
                returnInvoice.Description = $"{returnInvoice.Description} | دلیل ابطال: {reason}";
                returnInvoice.ModifiedAt = DateTime.UtcNow;

                _context.purchaseReturnInvoiceUW.Update(returnInvoice);
                await _context.SaveAsync();

                await transaction.CommitAsync(ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از خرید و سند حسابداری متناظر با موفقیت باطل شدند."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
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
