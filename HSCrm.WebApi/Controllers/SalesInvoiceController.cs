using Microsoft.AspNetCore.Authorization;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Exceptions;
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
    public class SalesInvoiceController : Controller
    {
        private readonly IUnitOfWork _context;
        private readonly IInventoryService _inventory;
        private readonly IAccountingService _accountingService;
        private readonly IPaymentSettlementService _paymentSettlementService;
        private readonly ICurrentFiscalYearService? _currentFiscalYearService;
        private readonly IInvoiceNumberGenerator? _invoiceNumberGenerator;
        private readonly IProjectAccessService? _projectAccess;

        public SalesInvoiceController(
            IUnitOfWork context,
            IInventoryService inventory,
            IPaymentSettlementService paymentSettlementService,
            IAccountingService accountingService,
            ICurrentFiscalYearService? currentFiscalYearService = null,
            IInvoiceNumberGenerator? invoiceNumberGenerator = null,
            IProjectAccessService? projectAccess = null)
        {
            _context = context;
            _inventory = inventory;
            _paymentSettlementService = paymentSettlementService;
            _accountingService = accountingService;
            _currentFiscalYearService = currentFiscalYearService;
            _invoiceNumberGenerator = invoiceNumberGenerator;
            _projectAccess = projectAccess;
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
                    ? await _invoiceNumberGenerator.GenerateNextSalesInvoiceNumberAsync(tenantId.Value, fyId)
                    : $"INV-{DateTime.UtcNow.Year}-0001");

            return new ApiResponse<object>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new
                {
                    nextInvoiceNumber = nextNumber,
                    pattern = tenant?.SalesInvoicePattern ?? "INV-{YEAR}-{SEQ:4}",
                    numberingMode = mode,
                    defaultVatPercent = tenant?.DefaultVatPercent ?? 0m,
                    defaultCostingMethod = tenant?.DefaultCostingMethod ?? InventoryCostingMethod.WeightedAverage,
                    invoiceFooterText = tenant?.InvoiceFooterText,
                    companyTitle = tenant?.TenantTitle,
                    logoUrl = tenant?.LogoUrl,
                    companyPhone = tenant?.CompanyPhone,
                    address = tenant?.Address,
                    taxId = tenant?.TaxId
                }
            };
        }

        [HttpGet("SettlementSummary/{id}")]
        public async Task<IActionResult> SettlementSummary(int id, CancellationToken ct = default)
        {
            var invoice = _context.salesInvoiceUW.GetById(id);
            if (invoice == null)
                return NotFound();

            var totalAmount = invoice.Total;

            var result = _paymentSettlementService.BuildSalesInvoiceSummary(id, totalAmount);
            return Ok(result);
        }

        [HttpGet("SettlementSummaries")]
        [Permission(Permissions.SalesInvoices.View)]
        public async Task<ApiResponse<List<HSCrm.Entities.Models.Dto.SettlementSummaryDto>>> GetSettlementSummaries(CancellationToken ct = default)
        {
            var query = _context.salesInvoiceUW.Get(x => !x.IsDeleted, true);
            var tenantId = User?.GetTenantId();
            var userId = User?.GetUserId();
            if (_projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId)
                && !await _projectAccess.HasFullProjectAccessAsync(userId, ct))
            {
                var allowedIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId.Value, userId, ct);
                query = query.Where(x => allowedIds.Contains(x.ProjectId));
            }
            var invoices = query.ToList();
            var summaries = invoices
                .Select(inv => _paymentSettlementService.BuildSalesInvoiceSummary(inv.Id, inv.Total))
                .ToList();

            return new ApiResponse<List<HSCrm.Entities.Models.Dto.SettlementSummaryDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "موفق",
                Data = summaries
            };
        }

        [HttpGet("GetSalesInvoices")]
        [Permission(Permissions.SalesInvoices.View)]
        public async Task<ApiResponse<IEnumerable<SalesInvoice>>> Get(CancellationToken ct = default)
        {
            var query = _context.salesInvoiceUW
                .Get(null, true, x => x.Project, x => x.Project.Customer, x => x.Tenant);
            var tenantId = User?.GetTenantId();
            var userId = User?.GetUserId();
            if (_projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId)
                && !await _projectAccess.HasFullProjectAccessAsync(userId, ct))
            {
                var allowedIds = await _projectAccess.GetAccessibleProjectIdsAsync(tenantId.Value, userId, ct);
                query = query.Where(x => allowedIds.Contains(x.ProjectId));
            }
            var invoices = query
                .OrderByDescending(x => x.IssueDate)
                .ThenByDescending(x => x.Id)
                .ToList();

            return new ApiResponse<IEnumerable<SalesInvoice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoices
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SalesInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.SalesInvoices.View)]
        public async Task<ApiResponse> GetById(int? invoiceId = null, int? id = null, CancellationToken ct = default)
        {
            var targetId = invoiceId ?? id ?? 0;
            var tenantId = User?.GetTenantId();

            var invoice = _context.salesInvoiceUW
                .Query()
                .AsNoTracking()
                .Include(x => x.Items)
                    .ThenInclude(i => i.Product)
                .Include(x => x.Project)
                    .ThenInclude(p => p.Customer)
                .Include(x => x.Warehouse)
                .Include(x => x.Tenant)
                .FirstOrDefault(x => x.Id == targetId && (!tenantId.HasValue || x.TenantId == tenantId.Value));

            if (invoice == null) return new ApiResponse<SalesInvoice>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoice
            };

            var userId = User?.GetUserId();
            if (_projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId)
                && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, invoice.ProjectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Forbidden,
                    Message = "شما عضو پروژه این فاکتور نیستید و مجاز به مشاهده آن نمی‌باشید."
                };
            }

            if (invoice.Items != null)
            {
                foreach (var item in invoice.Items)
                {
                    item.SalesInvoice = null!;
                }
            }

            return new ApiResponse<SalesInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoice
            };
        }

        [HttpPost("CreateSalesInvoice")]
        [Permission(Permissions.SalesInvoices.Create)]
        public async Task<ApiResponse> CreateSalesInvoice([FromBody] SalesInvoiceCreateModel model, [FromQuery] InventoryCostingMethod? costingMethod = null)
        {
            if (model == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "اطلاعات فاکتور ارسال نشده است."
                };
            }

            if (model.Items == null || model.Items.Count == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NoContent,
                Message = ApiResultStatusCode.NoContent.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tenantId = User?.GetTenantId() ?? (model.TenantId > 0 ? model.TenantId : (int?)null);
            if (!tenantId.HasValue)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شناسه مستاجر (TenantId) در درخواست موجود نیست."
                };
            }

            var actorUserId = User?.GetUserId();
            if (_projectAccess != null && !string.IsNullOrWhiteSpace(actorUserId)
                && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, actorUserId, model.ProjectId))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Forbidden,
                    Message = "شما عضو این پروژه نیستید و مجاز به ثبت فاکتور فروش برای آن نمی‌باشید."
                };
            }

            var tenant = _context.tenantUW?.GetById(tenantId.Value);
            var effectiveCostingMethod = costingMethod ?? tenant?.DefaultCostingMethod ?? InventoryCostingMethod.WeightedAverage;
            var numberingMode = tenant?.InvoiceNumberingMode ?? "Hybrid";

            int fiscalYearId = _currentFiscalYearService?.FiscalYearId
                ?? _context.fiscalYearUW?.Get(f => f.TenantId == tenantId.Value && f.IsCurrent && !f.IsClosed)
                                       .Select(f => (int?)f.Id)
                                       .FirstOrDefault()
                ?? 1;

            DateTime issueDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(model.IssueDate))
            {
                try
                {
                    issueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate);
                }
                catch
                {
                    issueDate = DateTime.UtcNow;
                }
            }

            if (tenant?.DocumentLockDate != null && issueDate.Date <= tenant.DocumentLockDate.Value.Date)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "ثبت فاکتور فروش در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                };
            }

            if (string.Equals(numberingMode, "Manual", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "در حالت شماره‌گذاری دستی، وارد کردن شماره فاکتور الزامی است."
                };
            }

            if (string.Equals(numberingMode, "Auto", StringComparison.OrdinalIgnoreCase) && _invoiceNumberGenerator != null)
            {
                model.InvoiceNumber = await _invoiceNumberGenerator.GenerateNextSalesInvoiceNumberAsync(tenantId.Value, fiscalYearId, issueDate);
            }
            else if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                if (_invoiceNumberGenerator != null)
                {
                    model.InvoiceNumber = await _invoiceNumberGenerator.GenerateNextSalesInvoiceNumberAsync(tenantId.Value, fiscalYearId, issueDate);
                }
                else
                {
                    model.InvoiceNumber = $"INV-{issueDate:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpper()}";
                }
            }

            var resolvedInvoiceNumber = model.InvoiceNumber!.Trim();
            model.InvoiceNumber = resolvedInvoiceNumber;

            var transaction = _context.BeginTransaction();

            try
            {
                bool invoiceNumberExists = false;
                try
                {
                    var existingQuery = _context.salesInvoiceUW?.Get(i => i.Number == resolvedInvoiceNumber && i.TenantId == tenantId.Value);
                    invoiceNumberExists = existingQuery != null && existingQuery.Any();
                }
                catch (InvalidOperationException)
                {
                    invoiceNumberExists = false;
                }

                if (invoiceNumberExists)
                {
                    transaction?.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                    };
                }

                decimal subtotal = model.Items.Sum(i => i.Quantity * i.UnitPrice);
                decimal itemsTotal = model.Items.Sum(i => (i.Quantity * i.UnitPrice) - i.Discount + i.Tax);

                decimal finalTotal = itemsTotal - model.InvoiceDiscount + model.InvoiceTax;

                if (finalTotal < 0)
                    finalTotal = 0;

                var userId = User?.GetUserId() ?? model.SellerUserId;

                var invoice = new SalesInvoice
                {
                    TenantId = tenantId.Value,
                    FiscalYearId = fiscalYearId,
                    WarehouseId = model.WarehouseId,
                    ProjectId = model.ProjectId,
                    Number = resolvedInvoiceNumber,
                    IssueDate = issueDate,
                    Status = model.Status,
                    Subtotal = subtotal,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = finalTotal,
                    SellerUserId = model.SellerUserId,
                    CreatedById = userId,
                    Description = "فاکتور فروش شماره " + resolvedInvoiceNumber,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var item in model.Items)
                {
                    invoice.Items.Add(new SalesInvoiceItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPriceSell = item.UnitPrice,
                        Discount = item.Discount,
                        Tax = item.Tax,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = userId,
                        TenantId = tenantId.Value,
                        LineTotal = (item.Quantity * item.UnitPrice) - item.Discount + item.Tax
                    });
                }

                _context.salesInvoiceUW.Create(invoice);
                await _context.SaveAsync();

                if (invoice.Status == InvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForSalesInvoice(invoice, effectiveCostingMethod);
                    await _accountingService.CreateVoucherForSalesInvoiceAsync(invoice, effectiveCostingMethod);
                    await _context.SaveAsync();
                }

                transaction?.Commit();

                return new ApiResponse<SalesInvoice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = invoice
                };
            }
            catch (NotEnoughInventoryException)
            {
                transaction?.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotEnoughInventory,
                    Message = ApiResultStatusCode.NotEnoughInventory.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
            catch (UnauthorizedAccessException ex)
            {
                transaction?.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ex.Message
                };
            }
            catch (Exception)
            {
                transaction?.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        [HttpGet("GetByProjectId")]
        [HttpGet("GetProjectInvoices")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SalesInvoiceModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.SalesInvoices.View)]
        public async Task<ApiResponse<IEnumerable<SalesInvoiceModel>>> GetByProjectId(int projectId, CancellationToken ct = default)
        {
            var tenantId = User?.GetTenantId();
            var userId = User?.GetUserId();
            if (_projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId)
                && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, projectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse<IEnumerable<SalesInvoiceModel>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Forbidden,
                    Message = "شما عضو این پروژه نیستید و مجاز به مشاهده فاکتورهای آن نمی‌باشید."
                };
            }

            var invoiceList = _context.salesInvoiceUW
                .Get(e => e.ProjectId == projectId,
                     asNoTracking: true,
                     includes: e => e.Project)
                .Select(e => new SalesInvoiceModel
                {
                    Id = e.Id,
                    Number = e.Number,
                    ProjectId = e.ProjectId,
                    IssueDate = e.IssueDate,
                    Status = e.Status,
                    Subtotal = e.Subtotal,
                    Tax = e.Tax,
                    Discount = e.Discount,
                    Total = e.Total,
                    Project = e.Project,
                }).ToList();

            if (!invoiceList.Any())
            {
                return new ApiResponse<IEnumerable<SalesInvoiceModel>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "هیچ فاکتور فروشی برای پروژه مورد نظر یافت نشد.",
                    Data = invoiceList
                };
            }

            return new ApiResponse<IEnumerable<SalesInvoiceModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoiceList
            };
        }

        [HttpPut("UpdateInvoice")]
        [Permission(Permissions.SalesInvoices.Edit)]
        public async Task<ApiResponse> Update([FromBody] SalesInvoiceEditModel model, [FromQuery] InventoryCostingMethod? costingMethod = null)
        {
            using var tr = _context.BeginTransaction();
            try
            {
                var invoice = _context.salesInvoiceUW
                    .Get(x => x.Id == model.InvoiceId, false, x => x.Items)
                    .SingleOrDefault();

                if (invoice == null) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                };

                var actorUserId = User?.GetUserId();
                if (_projectAccess != null && !string.IsNullOrWhiteSpace(actorUserId)
                    && !await _projectAccess.CanAccessProjectAsync(invoice.TenantId, actorUserId, invoice.ProjectId))
                {
                    tr.Rollback();
                    if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Forbidden,
                        Message = "شما عضو پروژه این فاکتور نیستید و مجاز به ویرایش آن نمی‌باشید."
                    };
                }

                var tenant = _context.tenantUW?.GetById(invoice.TenantId);
                if (tenant?.DocumentLockDate != null && (model.IssueDate.Date <= tenant.DocumentLockDate.Value.Date || invoice.IssueDate.Date <= tenant.DocumentLockDate.Value.Date))
                {
                    tr.Rollback();
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = "ویرایش فاکتور در تاریخ‌های قفل‌شده امکان‌پذیر نیست."
                    };
                }

                var effectiveCostingMethod = costingMethod ?? tenant?.DefaultCostingMethod ?? InventoryCostingMethod.WeightedAverage;
                var oldStatus = invoice.Status;
                var newStatus = (InvoiceStatus)model.Status;

                if (oldStatus == InvoiceStatus.Final)
                {
                    _inventory.RollbackInventory(invoice);
                    await _accountingService.CancelVoucherByReferenceAsync(
                        AccountingReferenceType.SalesInvoice,
                        invoice.Id,
                        "ویرایش فاکتور فروش");
                }

                _context.salesInvoiceItemUW.DeleteByRange(x => x.SalesInvoiceId == invoice.Id);
                invoice.Items.Clear();

                decimal subtotal = 0m;
                foreach (var item in model.Items)
                {
                    subtotal += item.UnitPrice * item.Quantity;
                    invoice.Items.Add(new SalesInvoiceItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPriceSell = item.UnitPrice,
                        LineTotal = item.UnitPrice * item.Quantity,
                        TenantId = User?.GetTenantId() ?? invoice.TenantId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User?.GetUserId()
                    });
                }

                invoice.Subtotal = subtotal;
                invoice.Total = subtotal - invoice.Discount + invoice.Tax;
                invoice.Status = newStatus;
                invoice.IssueDate = model.IssueDate;

                await _context.SaveAsync();

                if (newStatus == InvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForSalesInvoice(invoice, effectiveCostingMethod);
                    await _accountingService.CreateVoucherForSalesInvoiceAsync(invoice, effectiveCostingMethod);
                    await _context.SaveAsync();
                }

                tr.Commit();
                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
            catch (Exception)
            {
                tr.Rollback();
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPost("ConfirmInvoice")]
        [Permission(Permissions.SalesInvoices.Create)]
        public async Task<IActionResult> Confirm([FromForm] int invoiceId)
        {
            using var tran = _context.BeginTransaction();

            try
            {
                var tenantIdOpt = User?.GetTenantId();
                if (!tenantIdOpt.HasValue)
                    return BadRequest("شناسه مستاجر (TenantId) نامعتبر است.");

                var tenantId = tenantIdOpt.Value;
                var userId = User?.GetUserId();

                var invoice = (await _context.salesInvoiceUW.GetAsync(
                    x => x.Id == invoiceId && x.TenantId == tenantId,
                    asNoTracking: false,
                    includes: x => x.Items
                )).FirstOrDefault();

                if (invoice == null)
                    return NotFound("فاکتور یافت نشد");

                if (_projectAccess != null && !string.IsNullOrWhiteSpace(userId)
                    && !await _projectAccess.CanAccessProjectAsync(tenantId, userId, invoice.ProjectId))
                {
                    tran.Rollback();
                    return StatusCode(403, new ApiResponse { Status = false, StatusCode = ApiResultStatusCode.Forbidden, Message = "شما به پروژه این فاکتور دسترسی ندارید." });
                }

                var tenant = _context.tenantUW?.GetById(tenantId);
                if (tenant?.DocumentLockDate != null && invoice.IssueDate.Date <= tenant.DocumentLockDate.Value.Date)
                    return BadRequest("تأیید فاکتور در تاریخ‌های قفل‌شده امکان‌پذیر نیست.");

                if (invoice.Status == InvoiceStatus.Final)
                    return BadRequest("قبلاً تأیید شده");

                // ۱. بررسی اولیه موجودی تمام اقلام
                var inventoriesToUpdate = new List<(Inventory Inventory, SalesInvoiceItem Item)>();

                foreach (var item in invoice.Items)
                {
                    var inventory = (await _context.inventoryUW.GetAsync(
                        x => x.TenantId == tenantId && x.ProductId == item.ProductId,
                        asNoTracking: false
                    )).FirstOrDefault();

                    if (inventory == null || inventory.Quantity < item.Quantity)
                        return Conflict($"موجودی کالای {item.ProductId} کافی نیست");

                    inventoriesToUpdate.Add((inventory, item));
                }

                // ۲. کسر موجودی، محاسبه بهای تمام‌شده (COGS) و ثبت تراکنش انبار با WarehouseId معتبر
                foreach (var (inventory, item) in inventoriesToUpdate)
                {
                    item.UnitCost = inventory.AverageUnitCost;
                    item.TotalCost = Math.Round(item.Quantity * inventory.AverageUnitCost, 2);
                    inventory.Quantity -= item.Quantity;

                    _context.inventoryTransactionUW.Create(new InventoryTransaction
                    {
                        TenantId = tenantId,
                        FiscalYearId = invoice.FiscalYearId,
                        WarehouseId = inventory.WarehouseId,
                        ProductId = item.ProductId,
                        QuantityChange = -item.Quantity,
                        UnitCost = item.UnitCost,
                        TotalCost = item.TotalCost,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = userId,
                        Description = "تأیید فاکتور فروش شماره " + invoice.Number,
                        TransactionType = InventoryTransactionType.Sale,
                        ReferenceId = invoice.Id,
                        ReferenceType = InventoryReferenceType.SalesInvoice
                    });
                }

                invoice.Status = InvoiceStatus.Final;

                await _accountingService.CreateVoucherForSalesInvoiceAsync(invoice);
                await _context.SaveAsync();
                tran.Commit();

                return Ok(new { statusCode = 200, message = "فاکتور با موفقیت تأیید شد." });
            }
            catch (DbUpdateConcurrencyException)
            {
                tran.Rollback();
                return Conflict("تداخل همزمانی در ثبت موجودی، لطفاً مجدداً تلاش کنید.");
            }
            catch (Exception)
            {
                tran.Rollback();
                throw;
            }
        }


        [HttpGet("GetInvoiceDetail")]
        [Permission(Permissions.SalesInvoices.View)]
        public async Task<ApiResponse<IEnumerable<SalesInvoiceItem>>> GetDetail(int invoiceId, CancellationToken ct = default)
        {
            var header = _context.salesInvoiceUW.Get(x => x.Id == invoiceId, true).FirstOrDefault();
            var tenantId = User?.GetTenantId();
            var userId = User?.GetUserId();
            if (header != null && _projectAccess != null && tenantId.HasValue && !string.IsNullOrWhiteSpace(userId)
                && !await _projectAccess.CanAccessProjectAsync(tenantId.Value, userId, header.ProjectId, ct))
            {
                if (HttpContext != null) HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return new ApiResponse<IEnumerable<SalesInvoiceItem>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Forbidden,
                    Message = "شما به پروژه این فاکتور دسترسی ندارید."
                };
            }
            var invoice = _context.salesInvoiceItemUW.Get(i => i.SalesInvoiceId == invoiceId);

            if (invoice == null) return new ApiResponse<IEnumerable<SalesInvoiceItem>>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoice
            };

            return new ApiResponse<IEnumerable<SalesInvoiceItem>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoice
            };
        }
    }
}