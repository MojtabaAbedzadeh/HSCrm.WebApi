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
    [Route("api/[controller]")]
    [ApiController]
    public class SalesInvoiceController : Controller
    {
        private readonly IUnitOfWork _context;
        private readonly IInventoryService _inventory;
        private readonly IAccountingService _accountingService;
        private readonly IPaymentSettlementService _paymentSettlementService;

        public SalesInvoiceController(IUnitOfWork context, IInventoryService inventory, IPaymentSettlementService paymentSettlementService, IAccountingService accountingService)
        {
            _context = context;
            _inventory = inventory;
            _paymentSettlementService = paymentSettlementService;
            _accountingService = accountingService;
        }

        [HttpGet("SettlementSummary/{id}")]
        public IActionResult SettlementSummary(int id)
        {
            var invoice = _context.salesInvoiceUW.GetById(id);
            if (invoice == null)
                return NotFound();

            var totalAmount = invoice.Total;

            var result = _paymentSettlementService.BuildSalesInvoiceSummary(id, totalAmount);
            return Ok(result);
        }

        [HttpGet("GetSalesInvoices")]
        [Permission(Permissions.SalesInvoices.View)]
        public ApiResponse<IEnumerable<SalesInvoice>> Get()
        {
            var invoices = _context.salesInvoiceUW.Get(null, true, x => x.Project, x => x.Project.Customer, x => x.Tenant).ToList();

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
        public ApiResponse GetById(int invoiceId)
        {
            var invoice = _context.salesInvoiceUW.GetById(invoiceId);

            if (invoice == null) return new ApiResponse<SalesInvoice>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoice
            };

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
        public async Task<ApiResponse> CreateSalesInvoice([FromBody] SalesInvoiceCreateModel model, [FromQuery] InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage)
        {
            if (model.Items == null || model.Items.Count == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NoContent,
                Message = ApiResultStatusCode.NoContent.DisplayNameAttribute(DisplayProperty.Name)
            };

            using var transaction = _context.BeginTransaction();

            try
            {
                bool invoiceNumberExists = _context.salesInvoiceUW.Get().Any(i => i.Number == model.InvoiceNumber && i.TenantId == model.TenantId);

                if (invoiceNumberExists)
                {
                    transaction.Rollback();

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

                var invoice = new SalesInvoice
                {
                    TenantId = User.GetTenantId().Value,
                    WarehouseId = model.WarehouseId,
                    ProjectId = model.ProjectId,
                    Number = model.InvoiceNumber,
                    IssueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate),
                    Status = model.Status,
                    Subtotal = subtotal,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = finalTotal,
                    SellerUserId = model.SellerUserId,
                    CreatedById = User.GetUserId(),
                    Description = "فاکتور فروش شماره" + model.InvoiceNumber,
                    CreatedAt = DateTime.Now
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
                        CreatedAt = DateTime.Now,
                        CreatedById = User.GetUserId(),
                        TenantId = User.GetTenantId().Value,
                        LineTotal = (item.Quantity * item.UnitPrice) - item.Discount + item.Tax
                    });
                }

                _context.salesInvoiceUW.Create(invoice);
                await _context.SaveAsync();

                if (invoice.Status == InvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForSalesInvoice(invoice, costingMethod);
                    await _accountingService.CreateVoucherForSalesInvoiceAsync(invoice, costingMethod);
                    await _context.SaveAsync();
                }

                transaction.Commit();

                return new ApiResponse<SalesInvoice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
            catch (NotEnoughInventoryException)
            {
                transaction.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotEnoughInventory,
                    Message = ApiResultStatusCode.NotEnoughInventory.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
            catch (Exception)
            {
                transaction.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpGet("GetByProjectId")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(SalesInvoiceModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.SalesInvoices.View)]
        public ApiResponse<IEnumerable<SalesInvoiceModel>> GetByProjectId(int projectId)
        {
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
        public async Task<ApiResponse> Update([FromBody] SalesInvoiceEditModel model, [FromQuery] InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage)
        {
            using var tr = _context.BeginTransaction();
            try
            {
                var invoice = _context.salesInvoiceUW
                    .Get(x => x.Id == model.InvoiceId, true, x => x.Items)
                    .SingleOrDefault();

                if (invoice == null) return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                };

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
                        TenantId = User.GetTenantId().Value,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.GetUserId()
                    });
                }

                invoice.Subtotal = subtotal;
                invoice.Total = subtotal - invoice.Discount + invoice.Tax;
                invoice.Status = newStatus;
                invoice.IssueDate = model.IssueDate;

                await _context.SaveAsync();

                if (newStatus == InvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForSalesInvoice(invoice, costingMethod);
                    await _accountingService.CreateVoucherForSalesInvoiceAsync(invoice, costingMethod);
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
                var tenantId = User.GetTenantId().Value;
                var userId = User.GetUserId();

                var invoice = (await _context.salesInvoiceUW.GetAsync(
                    x => x.Id == invoiceId && x.TenantId == tenantId,
                    asNoTracking: false,
                    includes: x => x.Items
                )).FirstOrDefault();

                if (invoice == null)
                    return NotFound("فاکتور یافت نشد");

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
        public ApiResponse<IEnumerable<SalesInvoiceItem>> GetDetail(int invoiceId)
        {
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