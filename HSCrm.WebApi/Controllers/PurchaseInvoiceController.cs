using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;
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

        public PurchaseInvoiceController(IUnitOfWork context, IInventoryService inventory)
        {
            _context = context;
            _inventory = inventory;
        }

        [HttpGet("GetPurchaseInvoices")]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse<IEnumerable<PurchaseInvoice>> Get()
        {
            var invoices = _context.purchaseInvoiceUW.Get(null, true, x => x.Supplier, x => x.Tenant).ToList();

            return new ApiResponse<IEnumerable<PurchaseInvoice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = invoices
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse GetById(int InvoiceId)
        {
            var factor = _context.purchaseInvoiceUW.GetById(InvoiceId);

            if (factor == null) return new ApiResponse<PurchaseInvoice>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = factor
            };

            return new ApiResponse<PurchaseInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = factor
            };
        }

        [HttpGet("GetByProjectId")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.View)]
        public ApiResponse<IEnumerable<PurchaseInvoice>> GetByProjectId(int projectId)
        {
            var factor = _context.purchaseInvoiceUW.Get();

            if (factor == null) return new ApiResponse<IEnumerable<PurchaseInvoice>>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = factor
            };

            return new ApiResponse<IEnumerable<PurchaseInvoice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = factor
            };
        }

        [HttpPost("CreateInvoice")]
        [Permission(Permissions.PurchaseInvoices.Create)]
        public ApiResponse Create([FromBody] InvoiceCreateModel model)
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
                // ✅ 1. بررسی تکراری بودن شماره فاکتور (در همان Tenant)
                bool invoiceNumberExists = _context.purchaseInvoiceUW.Get().Any(i => i.Number == model.InvoiceNumber && i.TenantId == model.TenantId);

                if (invoiceNumberExists)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict, // 409
                        Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                    };
                }

                // ✅ 2. محاسبه مجموع آیتم‌ها
                decimal itemsTotal = model.Items.Sum(i => (i.Quantity * i.UnitPrice) - i.Discount + i.Tax);

                decimal finalTotal = itemsTotal - model.InvoiceDiscount + model.InvoiceTax;
                if (finalTotal < 0) finalTotal = 0;

                // ✅ 3. ساخت فاکتور
                var invoice = new PurchaseInvoice
                {
                    TenantId = User.GetTenantId().Value,
                    SupplierId = model.SupplierId,
                    WarehouseId = model.WarehouseId,
                    Number = model.InvoiceNumber,
                    IssueDate = ConvertDate.ConvertShamsiToMiladi(model.IssueDate),
                    Status = model.Status,
                    Discount = model.InvoiceDiscount,
                    Tax = model.InvoiceTax,
                    Total = finalTotal,
                    PurchaserUserId = model.PurchaserId,
                    CreatedById = User.GetUserId(),
                    CreatedAt = DateTime.Now
                };

                foreach (var item in model.Items)
                {
                    invoice.Items.Add(new PurchaseInvoiceItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPricePurchase = item.UnitPrice,
                        Discount = item.Discount,
                        Tax = item.Tax,
                        CreatedAt = DateTime.Now,
                        CreatedById = User.GetUserId(),
                        TenantId = User.GetTenantId().Value,
                        LineTotal = (item.Quantity * item.UnitPrice) - item.Discount + item.Tax
                    });
                }

                // ✅ 4. ثبت فاکتور
                _context.purchaseInvoiceUW.Create(invoice);
                _context.Save();

                // ✅ 5. اعمال موجودی فقط اگر نهایی بود
                if (invoice.Status == SalesInvoiceStatus.Final)
                {
                    _inventory.ApplyInventoryForPurchaseInvoice(invoice);
                    _context.Save();
                }

                // ✅ 6. Commit نهایی
                transaction.Commit();

                return new ApiResponse
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name)
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

        [HttpPut("UpdateInvoice")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.PurchaseInvoices.Edit)]
        public ApiResponse Update([FromBody] SalesInvoiceEditModel model)
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

                // به‌روزرسانی اطلاعات فاکتور
                //invoice.Number = model.InvoiceNumber;
                //invoice.PayStatus = model.Status;
                //invoice.IssueDate = model.InvoiceDate;


                // حذف جزئیات قبلی با DeleteByRange (بدون لود شدن داده‌ها)
                _context.purchaseInvoiceDetailUW.DeleteByRange(d => d.Id == model.InvoiceId);

                // ساخت جزئیات جدید - ProductRowPrice = Count × UnitPrice
                var newDetails = model.Items.Select(i => new PurchaseInvoiceItem
                {
                    Id = model.InvoiceId,
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId(),
                    TenantId = User.GetTenantId().Value,
                    UnitPricePurchase = i.UnitPrice * i.Quantity
                }).ToList();

                _context.purchaseInvoiceDetailUW.CreateRange(newDetails);

                // محاسبه جمع کل جدید
                invoice.Total = newDetails.Sum(x => x.UnitPricePurchase);

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
