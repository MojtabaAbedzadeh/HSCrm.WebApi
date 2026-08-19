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
        public ApiResponse<IEnumerable<PurchaseInvoiceModel>> Get(string supplierFullName = null, DateTime? fromDate = null, DateTime? toDate = null, decimal? minTotal = null,  decimal? maxTotal = null)
        {
            var query = _context.purchaseInvoiceUW
                .Get(null, true, x => x.Supplier, x => x.Tenant)
                .AsQueryable();

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

            var result = invoices.Select(x => new PurchaseInvoiceModel
            {
                Id = x.Id,
                Number = x.Number,
                SupplierId = x.SupplierId,
                Supplier = x.Supplier,
                IssueDate = x.IssueDate,
                PayStatus = x.PayStatus,
                Status = x.Status,
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

        [HttpGet("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ApiResponse<PurchaseInvoice>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse GetById(int InvoiceId)
        {
            var factor = _context.purchaseInvoiceUW
       .Query()
       .Include(x => x.Items)
           .ThenInclude(x => x.Product)
       .Include(x => x.Supplier)
       .Include(x => x.Warehouse)
       .FirstOrDefault(x => x.Id == InvoiceId);

            if (factor == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "فاکتور یافت نشد",
                    StatusCode = ApiResultStatusCode.NotFound
                };
            }

            if (factor.Items != null && factor.Items.Any())
            {
                foreach (var item in factor.Items)
                {
                    item.PurchaseInvoice = null;
                }
            }

            return new ApiResponse<PurchaseInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = factor
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
        public async Task<ApiResponse> Create([FromBody] PurchaseInvoiceCreateModel model)
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
                if (tenantId == null)
                    return new ApiResponse
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
                    .Get(x => x.Number == model.InvoiceNumber && x.FiscalYearId == fiscalYear.Value && x.TenantId == tenantId)
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

                var subtotal = model.Items.Sum(x => (x.Quantity * x.UnitPrice) - x.Discount + x.Tax);
                var total = subtotal - model.InvoiceDiscount + model.InvoiceTax;

                var invoice = new PurchaseInvoice
                {
                    Number = model.InvoiceNumber,
                    SupplierId = model.SupplierId,
                    WarehouseId = model.WarehouseId,
                    PurchaserUserId = User.GetUserId(),
                    Status = InvoiceStatus.Proforma,
                    IssueDate = issueDate,
                    Subtotal = subtotal,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = total,
                    FiscalYearId = fiscalYear.Value,
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTime.Now,
                    PayStatus = SettlementStatus.Unpaid,
                    Description = "فاکتور خرید از تامین کننده با شناسه " + model.SupplierId,
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
                        Tax = item.Tax,
                        CreatedAt = DateTime.Now,
                        CreatedById = User.GetUserId()
                    });
                }

                using var tx = _context.BeginTransaction();

                try
                {
                    _context.purchaseInvoiceUW.Create(invoice);
                    await _context.SaveAsync();

                    if (invoice.Status == InvoiceStatus.Final)
                    {
                        await _inventory.ApplyInventoryForPurchaseInvoice(invoice);
                        await _context.SaveAsync();
                    }

                    tx.Commit();

                    return new ApiResponse<int>
                    {
                        Status = true,
                        Message = "فاکتور خرید با موفقیت ثبت شد.",
                        StatusCode = ApiResultStatusCode.Success,
                        Data = invoice.Id
                    };
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
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
            if (model == null)
            {
                return new ApiResponse { Status = false, Message = "مدل فاکتور معتبر نیست.", StatusCode = ApiResultStatusCode.BadRequest };
            }

            if (model.Items == null || !model.Items.Any())
            {
                return new ApiResponse { Status = false, Message = "حداقل یک قلم کالا باید ثبت شود.", StatusCode = ApiResultStatusCode.BadRequest };
            }

            // لود کردن فاکتور به همراه آیتم‌های فعلی
            var invoice = _context.purchaseInvoiceUW
                .Get()
                .Include(x => x.Items)
                .FirstOrDefault(x => x.Id == model.InvoiceId);

            if (invoice == null)
            {
                return new ApiResponse { Status = false, Message = "فاکتور یافت نشد.", StatusCode = ApiResultStatusCode.NotFound };
            }

            // گارد ویرایش فاکتور نهایی
            if (invoice.Status == InvoiceStatus.Final)
            {
                return new ApiResponse { Status = false, Message = "فاکتور تایید نهایی شده و قابل ویرایش نیست.", StatusCode = ApiResultStatusCode.BadRequest };
            }

            using var tx = _context.BeginTransaction();

            try
            {
                // 1. حذف آیتم‌های قدیمی (با استفاده از متد موجود در ریپازیتوری تو)
                _context.purchaseInvoiceDetailUW.DeleteByRange(x => x.PurchaseInvoiceId == invoice.Id);

                // 2. به‌روزرسانی اطلاعات اصلی هدر فاکتور
                invoice.WarehouseId = model.WarehouseId;
                invoice.SupplierId = model.SupplierId;
                invoice.IssueDate = model.IssueDate;
                invoice.Number = model.InvoiceNumber?.Trim(); // طبق مدل تو: InvoiceNumber
                invoice.Status = model.Status;
                invoice.PurchaserUserId = model.PurchaserId; // طبق مدل تو: PurchaserId

                decimal subtotal = 0m;
                var newItems = new List<PurchaseInvoiceItem>();

                // 3. ایجاد آیتم‌های جدید و محاسبه مبالغ
                foreach (var item in model.Items)
                {
                    var lineDiscount = item.Discount;
                    var lineTax = item.Tax;
                    // در مدل تو UnitPrice است، در دیتابیس UnitPricePurchase
                    var lineTotal = (item.Quantity * item.UnitPrice) - lineDiscount + lineTax;

                    subtotal += (item.Quantity * item.UnitPrice);

                    newItems.Add(new PurchaseInvoiceItem
                    {
                        TenantId = invoice.TenantId,
                        PurchaseInvoiceId = invoice.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPricePurchase = item.UnitPrice, // مپ کردن UnitPrice به UnitPricePurchase
                        Discount = lineDiscount,
                        Tax = lineTax,
                        LineTotal = lineTotal,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = invoice.CreatedById
                    });
                }

                // انتساب مقادیر مالی کل فاکتور
                invoice.Subtotal = subtotal;
                invoice.Tax = model.InvoiceTax; // طبق مدل تو: InvoiceTax
                invoice.Discount = model.InvoiceDiscount; // طبق مدل تو: InvoiceDiscount
                invoice.Total = subtotal - invoice.Discount + invoice.Tax;

                // 4. درج در دیتابیس (با استفاده از متدهای صحیح شناسایی شده)
                _context.purchaseInvoiceDetailUW.CreateRange(newItems);
                _context.purchaseInvoiceUW.Update(invoice);

                await _context.SaveAsync();

                // 5. اعمال موجودی انبار در صورت نهایی شدن (Final)
                if (invoice.Status == InvoiceStatus.Final)
                {
                    invoice.Items = newItems; // پاس دادن لیست جدید برای پردازش موجودی
                    await _inventory.ApplyInventoryForPurchaseInvoice(invoice);
                    await _context.SaveAsync();
                }

                tx.Commit();

                return new ApiResponse
                {
                    Status = true,
                    Message = "فاکتور با موفقیت ویرایش شد.",
                    StatusCode = ApiResultStatusCode.Success
                };
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return new ApiResponse
                {
                    Status = false,
                    Message = $"خطا در ویرایش فاکتور: {ex.Message}",
                    StatusCode = ApiResultStatusCode.ServerError
                };
            }
        }

        [HttpPost("ConfirmInvoice/{id:int}")]
        public async Task<ApiResponse> ConfirmInvoice(int id)
        {
            using (var tx = _context.BeginTransaction())
            {
                try
                {
                    // دریافت فاکتور همراه با اقلام و با فعال بودن Tracking
                    var invoiceList = await _context.purchaseInvoiceUW.GetAsync(
                        predicate: x => x.Id == id,
                        asNoTracking: false,
                        includes: x => x.Items
                    );

                    var invoice = invoiceList.FirstOrDefault();

                    if (invoice == null)
                    {
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "فاکتور یافت نشد.",
                            StatusCode = ApiResultStatusCode.NotFound
                        };
                    }

                    if (invoice.Status == InvoiceStatus.Final)
                    {
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "فاکتور قبلاً تایید نهایی شده است.",
                            StatusCode = ApiResultStatusCode.BadRequest
                        };
                    }

                    if (invoice.Items == null || !invoice.Items.Any())
                    {
                        return new ApiResponse
                        {
                            Status = false,
                            Message = "فاکتور فاقد هرگونه قلم کالا جهت ورود به انبار است.",
                            StatusCode = ApiResultStatusCode.BadRequest
                        };
                    }

                    // تغییر وضعیت فاکتور به تایید نهایی
                    invoice.Status = InvoiceStatus.Final;
                    _context.purchaseInvoiceUW.Update(invoice);

                    // اعمال در انبار و ثبت تراکنش‌های ورود کالا
                    await _inventory.ApplyInventoryForPurchaseInvoice(invoice);

                    // ذخیره تغییرات
                    await _context.SaveAsync();

                    // ثبت قطعی تراکنش
                    tx.Commit();

                    return new ApiResponse
                    {
                        Status = true,
                        Message = "فاکتور با موفقیت تایید و موجودی انبار به‌روزرسانی شد.",
                        StatusCode = ApiResultStatusCode.Success
                    };
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return new ApiResponse
                    {
                        Status = false,
                        Message = ex.Message,
                        StatusCode = ApiResultStatusCode.ServerError
                    };
                }
            }
        }

        [HttpPost("CancelInvoice/{id}")]
        public async Task<ApiResponse> Cancel(int id, [FromBody] string? reason)
        {
            var invoice = await GetPurchaseInvoiceWithItemsAsync(id);

            if (invoice == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "فاکتور خرید یافت نشد",
                    StatusCode = ApiResultStatusCode.NotFound
                };
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "این فاکتور قبلاً باطل شده است",
                    StatusCode = ApiResultStatusCode.BadRequest
                };
            }

            if (invoice.Status == InvoiceStatus.Rejected)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "این فاکتور قبلاً رد شده است",
                    StatusCode = ApiResultStatusCode.BadRequest
                };
            }

            using var transaction = _context.BeginTransaction();

            try
            {
                if (invoice.Status == InvoiceStatus.Final)
                {
                    _inventory.RollbackInventoryForPurchaseInvoice(invoice);
                }

                invoice.Status = InvoiceStatus.Cancelled;

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    invoice.Description = string.IsNullOrWhiteSpace(invoice.Description)
                        ? $"Cancel Reason: {reason}"
                        : $"{invoice.Description}\nCancel Reason: {reason}";
                }

                _context.purchaseInvoiceUW.Update(invoice);
                await _context.SaveAsync();

                transaction.Commit();

                return new ApiResponse
                {
                    Status = true,
                    Message = "فاکتور خرید با موفقیت باطل شد",
                    StatusCode = ApiResultStatusCode.Success
                };
            }
            catch (Exception ex)
            {
                transaction.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    Message = $"خطا در ابطال فاکتور: {ex.Message}",
                    StatusCode = ApiResultStatusCode.ServerError
                };
            }
        }

        [HttpPost("RejectInvoice/{id}")]
        public async Task<ApiResponse> Reject(int id, [FromBody] string? reason)
        {
            var invoice = await GetPurchaseInvoiceWithItemsAsync(id);

            if (invoice == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "فاکتور خرید یافت نشد",
                    StatusCode = ApiResultStatusCode.NotFound
                };
            }

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "این فاکتور قبلاً باطل شده است",
                    StatusCode = ApiResultStatusCode.BadRequest
                };
            }

            if (invoice.Status == InvoiceStatus.Rejected)
            {
                return new ApiResponse
                {
                    Status = false,
                    Message = "این فاکتور قبلاً رد شده است",
                    StatusCode = ApiResultStatusCode.BadRequest
                };
            }

            using var transaction = _context.BeginTransaction();

            try
            {
                if (invoice.Status == InvoiceStatus.Final)
                {
                    _inventory.RollbackInventoryForPurchaseInvoice(invoice);
                }

                invoice.Status = InvoiceStatus.Rejected;

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    invoice.Description = string.IsNullOrWhiteSpace(invoice.Description)
                        ? $"Reject Reason: {reason}"
                        : $"{invoice.Description}\nReject Reason: {reason}";
                }

                _context.purchaseInvoiceUW.Update(invoice);
                await _context.SaveAsync();

                transaction.Commit();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "فاکتور خرید با موفقیت رد شد",
                };
            }
            catch (Exception ex)
            {
                transaction.Rollback();

                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = $"خطا در رد فاکتور: {ex.Message}",
                };
            }
        }

        private async Task<PurchaseInvoice?> GetPurchaseInvoiceWithItemsAsync(int id)
        {
            return await _context.purchaseInvoiceUW
                .Get(x => x.Id == id)
                .Include(x => x.Items)
                .FirstOrDefaultAsync();
        }
    }
}