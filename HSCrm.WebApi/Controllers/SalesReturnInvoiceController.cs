using Common.Permissions;
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
    [Route("api/[controller]")]
    [ApiController]
    public class SalesReturnInvoiceController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IInventoryService _inventory;
        private readonly IAccountingService _accountingService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly ILogger<SalesReturnInvoiceController> _logger;

        public SalesReturnInvoiceController(
            IUnitOfWork context,
            IInventoryService inventory,
            IAccountingService accountingService,
            ICurrentFiscalYearService currentFiscalYearService,
            ILogger<SalesReturnInvoiceController> logger)
        {
            _context = context;
            _inventory = inventory;
            _accountingService = accountingService;
            _currentFiscalYearService = currentFiscalYearService;
            _logger = logger;
        }

        [HttpGet("GetSalesReturnInvoices")]
        [Permission(Permissions.SalesReturnInvoices.View)]
        public ApiResponse<IEnumerable<SalesReturnInvoiceModel>> Get(
            string? customerFullName = null,
            int? salesInvoiceId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _context.salesReturnInvoiceUW
                .Query()
                .AsNoTracking()
                .Include(x => x.Items)
                .Include(x => x.SalesInvoice)
                .Include(x => x.Warehouse)
                .Include(x => x.Project)
                    .ThenInclude(p => p.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(customerFullName))
            {
                query = query.Where(x => x.Project != null && x.Project.Customer != null && x.Project.Customer.Name.Contains(customerFullName));
            }

            if (salesInvoiceId.HasValue)
            {
                query = query.Where(x => x.SalesInvoiceId == salesInvoiceId.Value);
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

            var result = list.Select(x => new SalesReturnInvoiceModel
            {
                Id = x.Id,
                Number = x.Number,
                SalesInvoiceId = x.SalesInvoiceId,
                OriginalSalesInvoiceNumber = x.SalesInvoice?.Number,
                ProjectId = x.ProjectId,
                ProjectTitle = x.Project?.ProjectTitle,
                CustomerId = x.Project?.CustomerId,
                CustomerName = x.Project?.Customer?.Name,
                WarehouseId = x.WarehouseId,
                WarehouseTitle = x.Warehouse?.Title,
                IssueDate = x.IssueDate,
                Status = x.Status,
                Subtotal = x.Subtotal,
                Tax = x.Tax,
                Discount = x.Discount,
                Total = x.Total,
                TotalReversedCogs = x.Items?.Sum(i => i.TotalCost) ?? 0m,
                Description = x.Description
            }).ToList();

            return new ApiResponse<IEnumerable<SalesReturnInvoiceModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = result
            };
        }

        [HttpGet("{id}")]
        [Permission(Permissions.SalesReturnInvoices.View)]
        public ApiResponse GetById(int id)
        {
            var returnInvoice = _context.salesReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                    .ThenInclude(i => i.Product)
                .Include(x => x.SalesInvoice)
                .Include(x => x.Project)
                    .ThenInclude(p => p.Customer)
                .Include(x => x.Warehouse)
                .FirstOrDefault(x => x.Id == id);

            if (returnInvoice == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "فاکتور برگشت از فروش یافت نشد.",
                    StatusCode = ApiResultStatusCode.NotFound
                };
            }

            if (returnInvoice.Items != null)
            {
                foreach (var item in returnInvoice.Items)
                {
                    item.SalesReturnInvoice = null!;
                }
            }

            return new ApiResponse<SalesReturnInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = returnInvoice
            };
        }

        [HttpPost("CreateSalesReturnInvoice")]
        [Permission(Permissions.SalesReturnInvoices.Create)]
        public async Task<IActionResult> Create(
            [FromBody] SalesReturnInvoiceCreateModel model,
            CancellationToken ct = default)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Items == null || !model.Items.Any())
                return BadRequest(new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "فاکتور برگشت از فروش باید حداقل یک ردیف کالا داشته باشد."
                });

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

            // اگر فاکتور فروش مرجع مشخص شده باشد، کنترل عدم برگشت بیش از تعداد فروخته‌شده
            if (model.SalesInvoiceId.HasValue && model.SalesInvoiceId.Value > 0)
            {
                var origInvoice = await _context.salesInvoiceUW
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.Id == model.SalesInvoiceId.Value, ct);

                if (origInvoice == null)
                {
                    return BadRequest(new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = "فاکتور فروش مرجع یافت نشد."
                    });
                }

                var previousReturns = await _context.salesReturnInvoiceUW
                    .Query()
                    .AsNoTracking()
                    .Include(x => x.Items)
                    .Where(x => x.SalesInvoiceId == model.SalesInvoiceId.Value && x.Status == InvoiceStatus.Final && !x.IsDeleted)
                    .ToListAsync(ct);

                foreach (var reqItem in model.Items)
                {
                    var soldQty = origInvoice.Items
                        .Where(x => x.ProductId == reqItem.ProductId)
                        .Sum(x => x.Quantity);

                    var alreadyReturnedQty = previousReturns
                        .SelectMany(r => r.Items)
                        .Where(i => i.ProductId == reqItem.ProductId)
                        .Sum(i => i.Quantity);

                    if (reqItem.Quantity + alreadyReturnedQty > soldQty)
                    {
                        return BadRequest(new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = $"تعداد برگشتی کالا (شناسه {reqItem.ProductId}) بیشتر از تعداد قابل برگشت فاکتور فروش مرجع است. (فروخته‌شده: {soldQty}، برگشت قبلی: {alreadyReturnedQty})"
                        });
                    }
                }
            }

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                var returnInvoice = new SalesReturnInvoice
                {
                    Number = model.InvoiceNumber.CleanName(),
                    SalesInvoiceId = model.SalesInvoiceId,
                    ProjectId = model.ProjectId,
                    WarehouseId = model.WarehouseId,
                    TenantId = model.TenantId,
                    FiscalYearId = fiscalYearId.Value,
                    IssueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate),
                    SellerUserId = model.SellerUserId,
                    Status = model.Status,
                    CreatedById = model.SellerUserId,
                    CreatedAt = DateTime.UtcNow,
                    Description = model.Description ?? $"فاکتور برگشت از فروش شماره {model.InvoiceNumber}",
                    Items = new List<SalesReturnInvoiceItem>()
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

                    returnInvoice.Items.Add(new SalesReturnInvoiceItem
                    {
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        UnitPriceReturn = itemDto.UnitPrice,
                        Discount = itemDto.Discount,
                        Tax = itemDto.Tax,
                        LineTotal = lineTotal,
                        UnitCost = itemDto.UnitCost ?? 0m,
                        TotalCost = itemDto.UnitCost.HasValue ? Math.Round(itemDto.Quantity * itemDto.UnitCost.Value, 2) : 0m,
                        TenantId = model.TenantId,
                        CreatedById = model.SellerUserId,
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

                await _context.salesReturnInvoiceUW.CreateAsync(returnInvoice, ct);
                await _context.SaveAsync();

                if (returnInvoice.Status == InvoiceStatus.Final)
                {
                    // ۱. ورود مجدد کالا به انبار و محاسبه بهای تمام‌شده برگشتی
                    await _inventory.ApplyInventoryForSalesReturnAsync(returnInvoice, ct);
                    await _context.SaveAsync();

                    // ۲. صدور خودکار سند حسابداری دوبل (معکوس کردن درآمد فروش + معکوس کردن COGS)
                    await _accountingService.CreateVoucherForSalesReturnAsync(returnInvoice, ct);
                }

                await transaction.CommitAsync(ct);

                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از فروش با موفقیت ثبت شد و آثار انبار و حسابداری اعمال گردید."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error in SalesReturnInvoiceController.Create");
                return StatusCode(500, new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ex.Message
                });
            }
        }

        [HttpPost("Confirm/{id}")]
        [Permission(Permissions.SalesReturnInvoices.Approve)]
        public async Task<IActionResult> Confirm(int id, CancellationToken ct = default)
        {
            var returnInvoice = await _context.salesReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (returnInvoice == null)
                return NotFound("فاکتور برگشت از فروش یافت نشد.");

            if (returnInvoice.Status != InvoiceStatus.Proforma)
                return BadRequest("فقط پیش‌فاکتور برگشتی قابل تأیید نهایی است.");

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                returnInvoice.Status = InvoiceStatus.Final;
                returnInvoice.ModifiedAt = DateTime.UtcNow;

                await _inventory.ApplyInventoryForSalesReturnAsync(returnInvoice, ct);
                _context.salesReturnInvoiceUW.Update(returnInvoice);
                await _context.SaveAsync();

                await _accountingService.CreateVoucherForSalesReturnAsync(returnInvoice, ct);

                await transaction.CommitAsync(ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از فروش نهایی شد و سند انبار و حسابداری صادر گردید."
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
        [Permission(Permissions.SalesReturnInvoices.Cancel)]
        public async Task<IActionResult> Cancel(int id, [FromBody] string reason, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return BadRequest("ذکر دلیل ابطال الزامی است.");

            var returnInvoice = await _context.salesReturnInvoiceUW
                .Query()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            if (returnInvoice == null)
                return NotFound("فاکتور برگشت از فروش یافت نشد.");

            if (returnInvoice.Status == InvoiceStatus.Cancelled)
                return BadRequest("این فاکتور برگشتی قبلاً باطل شده است.");

            using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (returnInvoice.Status == InvoiceStatus.Final)
                {
                    _inventory.RollbackInventoryForSalesReturn(returnInvoice);
                    await _accountingService.CancelVoucherByReferenceAsync(
                        AccountingReferenceType.SalesReturnInvoice,
                        returnInvoice.Id,
                        reason,
                        ct);
                }

                returnInvoice.Status = InvoiceStatus.Cancelled;
                returnInvoice.Description = $"{returnInvoice.Description} | دلیل ابطال: {reason}";
                returnInvoice.ModifiedAt = DateTime.UtcNow;

                _context.salesReturnInvoiceUW.Update(returnInvoice);
                await _context.SaveAsync();

                await transaction.CommitAsync(ct);
                return Ok(new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور برگشت از فروش و سند حسابداری متناظر با موفقیت باطل شدند."
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
