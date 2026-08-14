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
using System.Globalization;
using System.Linq.Expressions;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseInvoiceController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IInventoryService _inventory;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly IPaymentSettlementService _paymentSettlementService;

        public PurchaseInvoiceController(IUnitOfWork context, IInventoryService inventory, ICurrentFiscalYearService currentFiscalYearService, IPaymentSettlementService paymentSettlementService)
        {
            _context = context;
            _inventory = inventory;
            _currentFiscalYearService = currentFiscalYearService;
            _paymentSettlementService = paymentSettlementService;
        }

        [HttpGet("SettlementSummary/{id}")]
        public IActionResult SettlementSummary(int id)
        {
            var invoice = _context.purchaseInvoiceUW.GetById(id);
            if (invoice == null)
                return NotFound();

            var totalAmount = invoice.Total;
            var result = _paymentSettlementService.BuildPurchaseInvoiceSummary(id, totalAmount);
            return Ok(result);
        }

        [HttpGet("GetPurchaseInvoices")]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse<IEnumerable<PurchaseInvoiceModel>> Get(
            string supplierFullName = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            decimal? minTotal = null,
            decimal? maxTotal = null)
        {
            // بارگذاری ارتباطات مورد نیاز شامل Supplier
            var query = _context.purchaseInvoiceUW.Get(null, true, x => x.Supplier, x => x.Tenant).AsQueryable();
            if (!string.IsNullOrEmpty(supplierFullName))
            {
                query = query.Where(x => x.Supplier.SupplierFullName.Contains(supplierFullName));
            }
            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(x => x.IssueDate >= startDate);
            }
            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                query = query.Where(x => x.IssueDate < endDateExclusive);
            }
            if (minTotal.HasValue)
            {
                query = query.Where(x => x.Total >= minTotal.Value);
            }
            if (maxTotal.HasValue)
            {
                query = query.Where(x => x.Total <= maxTotal.Value);
            }

            var invoices = query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .ToList();

            // نگاشت فاکتورها به مدل DTO خروجی جهت محاسبه مانده و انتقال به ویو
            var result = invoices.Select(x => new PurchaseInvoiceModel
            {
                Id = x.Id,
                Number = x.Number,
                SupplierId = x.SupplierId,
                Supplier = x.Supplier,
                IssueDate = x.IssueDate,
                PayStatus = x.PayStatus,
                PaidAmount = x.PaidAmount,
                Subtotal = x.Subtotal,
                Tax = x.Tax,
                Discount = x.Discount,
                Total = x.Total
            }).ToList();

            return new ApiResponse<IEnumerable<PurchaseInvoiceModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = result
            };
        }


        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse GetById(int InvoiceId)
        {
            // استفاده از متد Get ریپازیتوری به همراه Expressions برای Include کردن ارتباطات به صورت Type-safe
            var factor = _context.purchaseInvoiceUW.Get(
                x => x.Id == InvoiceId,
                includes: new Expression<Func<PurchaseInvoice, object>>[]
                {
                    x => x.Items,
                    x => x.Supplier,
                    x => x.Warehouse
                }
            ).FirstOrDefault();

            if (factor == null)
            {
                return new ApiResponse { Status = false, Message = "فاکتور یافت نشد" };
            }

            if (factor.Items != null)
            {
                foreach (var item in factor.Items)
                {
                    // قطع حلقه تکرار برای جلوگیری از خطای سریالایز JSON
                    item.PurchaseInvoice = null;

                    // لود اطلاعات محصول
                    var product = _context.productUW.GetById(item.ProductId);
                    if (product != null)
                    {
                        item.Product = product;
                    }
                }
            }

            return new ApiResponse<PurchaseInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = factor // حتماً از نام Data استفاده کن نه AdditionalData
            };
        }


        [HttpGet("GetBySupplierId")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse<IEnumerable<PurchaseInvoice>> GetBySupplierId(int supplierId)
        {
            // ✅ فیلتر بر اساس تامین‌کننده (Supplier) به جای پروژه
            var factors = _context.purchaseInvoiceUW.Get(x => x.SupplierId == supplierId).ToList();

            if (factors == null || !factors.Any())
            {
                return new ApiResponse<IEnumerable<PurchaseInvoice>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "هیچ فاکتور خریدی برای تامین‌کننده مورد نظر یافت نشد.",
                    Data = factors
                };
            }

            return new ApiResponse<IEnumerable<PurchaseInvoice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = factors
            };
        }

        [HttpPost("CreateInvoice")]
        public ApiResponse Create([FromBody] PurchaseInvoiceCreateModel model)
        {
            try
            {
                if (model == null)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "مدل فاکتور معتبر نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                if (model.Items == null || !model.Items.Any())
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "حداقل یک قلم کالا باید ثبت شود.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                var tenantId = User.GetTenantId();
                if (tenantId == null) return new ApiResponse
                    {
                        Status = false,
                        Message = "شناسه شرکت معتبر نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "شماره فاکتور الزامی است.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                var fiscalYear = _currentFiscalYearService.FiscalYearId;
                if (fiscalYear == null)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "سال مالی فعال یافت نشد.",
                        StatusCode = ApiResultStatusCode.NotFound
                    };

                var existsInvoice = _context.purchaseInvoiceUW
                    .Get(x => x.Number == model.InvoiceNumber && x.FiscalYearId == fiscalYear.Value)
                    .Any();

                if (existsInvoice)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "شماره فاکتور تکراری است.",
                        StatusCode = ApiResultStatusCode.Conflict
                    };

                if (model.SupplierId <= 0)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "تأمین‌کننده معتبر نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                if (model.WarehouseId <= 0)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "انبار معتبر نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                if (!DateTime.TryParseExact(model.IssueDate, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issueDate))
                    issueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate);

                // محاسبات
                var subtotal = model.Items.Sum(x => (x.Quantity * x.UnitPrice) - x.Discount + x.Tax);
                var total = subtotal - model.InvoiceDiscount + model.InvoiceTax;

                var invoice = new PurchaseInvoice
                {
                    Number = model.InvoiceNumber,
                    SupplierId = model.SupplierId,
                    WarehouseId = model.WarehouseId,
                    PurchaserUserId = User.GetUserId(),
                    Status = model.Status,
                    IssueDate = issueDate,
                    Subtotal = subtotal,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = total,
                    FiscalYearId = fiscalYear.Value,
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTime.Now,
                    PayStatus = SettlementStatus.Unpaid,
                    PaidAmount = 0
                };

                foreach (var item in model.Items)
                {
                    if (item.ProductId <= 0)
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "کالای نامعتبر در آیتم‌ها وجود دارد.",
                            StatusCode = ApiResultStatusCode.BadRequest
                        };

                    if (item.Quantity <= 0)
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "مقدار کالا باید بزرگتر از صفر باشد.",
                            StatusCode = ApiResultStatusCode.BadRequest
                        };

                    if (item.UnitPrice < 0)
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "قیمت واحد نمی‌تواند منفی باشد.",
                            StatusCode = ApiResultStatusCode.BadRequest
                        };

                    invoice.Items.Add(new PurchaseInvoiceItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPricePurchase = item.UnitPrice,
                        Discount = item.Discount,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = User.GetUserId(),
                        Tax = item.Tax
                    });
                }

                _context.purchaseInvoiceUW.Create(invoice);
                _context.SaveAsync();

                // فقط در وضعیت Final اثر انبار اعمال می‌شود
                if (invoice.Status == SalesInvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForPurchaseInvoice(invoice);
                    _context.SaveAsync();
                }

                return new ApiResponse<int>
                {
                    Status = true,
                    Message = "فاکتور خرید با موفقیت ثبت شد.",
                    StatusCode = ApiResultStatusCode.Success,
                    Data = invoice.Id
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = ApiResultStatusCode.ServerError
                };
            }
        }

        [HttpPost("ConfirmInvoice/{id}")]
        public ApiResponse ConfirmInvoice(int id)
        {
            try
            {
                var invoice = _context.purchaseInvoiceUW.GetById(id);
                if (invoice == null)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "فاکتور یافت نشد.",
                        StatusCode = ApiResultStatusCode.NotFound
                    };

                if (invoice.Status == SalesInvoiceStatus.Final)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "فاکتور قبلاً تایید نهایی شده است.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                invoice.Status = SalesInvoiceStatus.Final;
                _inventory.ApplyInventoryForPurchaseInvoice(invoice);

                _context.purchaseInvoiceUW.Update(invoice);
                _context.SaveAsync();

                return new ApiResponse
                {
                    Status = true,
                    Message = "فاکتور با موفقیت تایید شد.",
                    StatusCode = ApiResultStatusCode.Success
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = ApiResultStatusCode.ServerError
                };
            }
        }
        [HttpPut("UpdateInvoice")]
        public async Task<ApiResponse> Update([FromBody] PurchaseInvoiceEditModel model)
        {
            try
            {
                if (model == null)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "مدل فاکتور معتبر نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                var invoice = _context.purchaseInvoiceUW
                    .Get()
                    .Include(x => x.Items)
                    .FirstOrDefault(x => x.Id == model.InvoiceId);

                if (invoice == null)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "فاکتور یافت نشد.",
                        StatusCode = ApiResultStatusCode.NotFound
                    };

                if (invoice.Status == SalesInvoiceStatus.Final)
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "فاکتور تایید نهایی شده و قابل ویرایش نیست.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                if (model.Items == null || !model.Items.Any())
                    return new ApiResponse
                    {
                        Status = false,
                        Message = "حداقل یک قلم کالا باید ثبت شود.",
                        StatusCode = ApiResultStatusCode.BadRequest
                    };

                var subtotal = model.Items.Sum(x => (x.Quantity * x.UnitPrice) - x.Discount + x.Tax);
                var total = subtotal - model.InvoiceDiscount + model.InvoiceTax;

                invoice.Number = model.InvoiceNumber;
                invoice.SupplierId = model.SupplierId;
                invoice.WarehouseId = model.WarehouseId;
                invoice.IssueDate = model.IssueDate;
                invoice.Status = model.Status;
                invoice.PurchaserUserId = model.PurchaserId;
                invoice.Subtotal = subtotal;
                invoice.Discount = model.InvoiceDiscount;
                invoice.Tax = model.InvoiceTax;
                invoice.Total = total;
                invoice.ModifiedAt = DateTime.UtcNow;
                invoice.ModifiedById = User.GetUserId();

                // حذف آیتم‌های قبلی
                if (invoice.Items != null && invoice.Items.Any())
                {
                    _context.purchaseInvoiceDetailUW.DeleteByRange(x => x.PurchaseInvoiceId == invoice.Id);
                }

                // اگر null بود، مقداردهی کن
                invoice.Items = new List<PurchaseInvoiceItem>();

                foreach (var item in model.Items)
                {
                    invoice.Items.Add(new PurchaseInvoiceItem
                    {
                        PurchaseInvoiceId = invoice.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPricePurchase = item.UnitPrice,
                        Discount = item.Discount,
                        Tax = item.Tax,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                _context.purchaseInvoiceUW.Update(invoice);
                await _context.SaveAsync();

                return new ApiResponse
                {
                    Status = true,
                    Message = "فاکتور با موفقیت ویرایش شد.",
                    StatusCode = ApiResultStatusCode.Success
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = ex.Message,
                    StatusCode = ApiResultStatusCode.ServerError
                };
            }
        }

    }
}