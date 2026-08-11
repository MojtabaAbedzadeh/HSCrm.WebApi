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
        [Permission(Permissions.PurchaseInvoices.Create)]
        public ApiResponse Create([FromBody] PurchaseInvoiceCreateModel model)
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

            if (model.Items == null || model.Items.Count == 0)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NoContent,
                    Message = ApiResultStatusCode.NoContent
                        .DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            if (User.GetTenantId() == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized
                        .DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            if (string.IsNullOrWhiteSpace(model.InvoiceNumber))
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شماره فاکتور الزامی است."
                };
            }

            using var transaction = _context.BeginTransaction();

            try
            {
                var tenantId = User.GetTenantId().Value;

                var userId = User.GetUserId();

                var fiscalYearId = _currentFiscalYearService.FiscalYearId;

                if (!fiscalYearId.HasValue)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = "سال مالی جاری مشخص نشده است."
                    };
                }

                bool invoiceNumberExists = _context.purchaseInvoiceUW
                        .Get()
                        .IgnoreQueryFilters()
                        .Any(invoice =>
                            invoice.Number == model.InvoiceNumber &&
                            invoice.TenantId == tenantId &&
                            invoice.FiscalYearId == fiscalYearId.Value);

                if (invoiceNumberExists)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = ApiResultStatusCode.Conflict
                            .DisplayNameAttribute(DisplayProperty.Name)
                    };
                }

                decimal itemsTotal = model.Items.Sum(item =>
                        (item.Quantity * item.UnitPrice)
                        - item.Discount
                        + item.Tax);

                decimal finalTotal = itemsTotal - model.InvoiceDiscount + model.InvoiceTax;

                if (finalTotal < 0)
                {
                    finalTotal = 0;
                }

                /*
                 * اعتبارسنجی وضعیت پرداخت
                 *
                 * 0 = بدون پرداخت
                 * 1 = پرداخت کامل
                 * 2 = پرداخت بخشی
                 */

                if (model.PaymentStatus < 0 || model.PaymentStatus > 2)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = "وضعیت پرداخت معتبر نیست."
                    };
                }

                decimal paidAmount = model.PaidAmount;

                if (paidAmount < 0)
                {
                    paidAmount = 0;
                }

                if (model.PaymentStatus == 0)
                {
                    paidAmount = 0;
                }
                else if (model.PaymentStatus == 1)
                {
                    paidAmount = finalTotal;
                }
                else if (model.PaymentStatus == 2)
                {
                    if (paidAmount <= 0)
                    {
                        transaction.Rollback();

                        return new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "برای پرداخت بخشی، مبلغ پرداختی باید بیشتر از صفر باشد."
                        };
                    }

                    if (paidAmount >= finalTotal)
                    {
                        transaction.Rollback();

                        return new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "در پرداخت بخشی، مبلغ پرداختی باید کمتر از مبلغ کل فاکتور باشد."
                        };
                    }
                }

                if (paidAmount > finalTotal)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = "مبلغ پرداختی نمی‌تواند بیشتر از مبلغ کل فاکتور باشد."
                    };
                }

                var invoice = new PurchaseInvoice
                {
                    TenantId = tenantId,
                    FiscalYearId = fiscalYearId.Value,
                    SupplierId = model.SupplierId,
                    WarehouseId = model.WarehouseId,
                    Number = model.InvoiceNumber,
                    IssueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate),
                    Status = model.Status,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = finalTotal,
                    PayStatus = (SettlementStatus)model.PaymentStatus,
                    PaidAmount = model.PaidAmount,
                    PurchaserUserId = model.PurchaserId,
                    CreatedById = userId,
                    CreatedAt = DateTime.Now
                };

                foreach (var item in model.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        transaction.Rollback();

                        return new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "تعداد محصول باید بیشتر از صفر باشد."
                        };
                    }

                    if (item.UnitPrice < 0 ||
                        item.Discount < 0 ||
                        item.Tax < 0)
                    {
                        transaction.Rollback();

                        return new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "مقادیر مبلغ، تخفیف و مالیات نمی‌توانند منفی باشند."
                        };
                    }

                    invoice.Items.Add(new PurchaseInvoiceItem
                    {
                        ProductId = item.ProductId,

                        Quantity = item.Quantity,

                        UnitPricePurchase = item.UnitPrice,

                        Discount = item.Discount,

                        Tax = item.Tax,

                        LineTotal =
                            (item.Quantity * item.UnitPrice)
                            - item.Discount
                            + item.Tax,

                        TenantId = tenantId,

                        CreatedById = userId,

                        CreatedAt = DateTime.Now
                    });
                }

                _context.purchaseInvoiceUW.Create(invoice);

                /*
                 * ابتدا فاکتور ذخیره می‌شود تا invoice.Id ساخته شود.
                 */
                _context.Save();

                if (invoice.Status == SalesInvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForPurchaseInvoice(invoice);

                    _context.Save();
                }

                /*
                 * اگر مبلغی پرداخت شده باشد، Payment به همان فاکتور متصل می‌شود.
                 */
                if (paidAmount > 0)
                {
                    var payment = new Payment
                    {
                        TenantId = tenantId,

                        FiscalYearId = fiscalYearId.Value,

                        PurchaseInvoiceId = invoice.Id,

                        PaymentDate = DateTime.Now,

                        Amount = paidAmount,

                        Method =
                            string.IsNullOrWhiteSpace(model.PaymentMethod)
                                ? "Cash"
                                : model.PaymentMethod.Trim(),

                        CreatedById = userId,

                        CreatedAt = DateTime.Now
                    };

                    _context.paymentUW.Create(payment);

                    _context.Save();
                }

                transaction.Commit();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success
                        .DisplayNameAttribute(DisplayProperty.Name)
                };
            }
            catch (Exception)
            {
                transaction.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError
                        .DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPut("UpdateInvoice")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.Edit)]
        public ApiResponse Update([FromBody] PurchaseInvoiceEditModel model)
        {
            if (model.InvoiceId == 0 || model.Items == null)
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

            try
            {
                var invoice = _context.purchaseInvoiceUW.GetById(model.InvoiceId);

                if (invoice == null)
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                    };

                // حذف جزئیات قبلی با DeleteByRange
                _context.purchaseInvoiceDetailUW.DeleteByRange(d => d.PurchaseInvoiceId == model.InvoiceId);

                var newDetails = model.Items.Select(i => new PurchaseInvoiceItem
                {
                    PurchaseInvoiceId = model.InvoiceId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPricePurchase = i.UnitPrice,
                    Discount = i.Discount,
                    Tax = i.Tax,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId(),
                    TenantId = User.GetTenantId().Value,
                    LineTotal = (i.Quantity * i.UnitPrice) - i.Discount + i.Tax
                }).ToList();

                _context.purchaseInvoiceDetailUW.CreateRange(newDetails);

                decimal itemsTotal = newDetails.Sum(x => x.LineTotal);
                decimal finalTotal = itemsTotal - model.InvoiceDiscount + model.InvoiceTax;
                if (finalTotal < 0) finalTotal = 0;

                invoice.SupplierId = model.SupplierId;
                invoice.WarehouseId = model.WarehouseId;
                invoice.Number = model.InvoiceNumber;
                invoice.IssueDate = model.IssueDate;
                invoice.Status = model.Status;
                invoice.Tax = model.InvoiceTax;
                invoice.Discount = model.InvoiceDiscount;
                invoice.Total = finalTotal;

                _context.purchaseInvoiceUW.Update(invoice);
                _context.Save();

                return new ApiResponse<PurchaseInvoice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = invoice
                };
            }
            catch
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }
    }
}
