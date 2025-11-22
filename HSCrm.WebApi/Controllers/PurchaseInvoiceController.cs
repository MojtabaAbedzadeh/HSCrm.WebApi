using Azure.Core;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Index.HPRtree;
using System.Numerics;
using System.Transactions;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseInvoiceController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public PurchaseInvoiceController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetFactors")]
        public ApiResponse<IEnumerable<PurchaseInvoice>> Get()
        {
            var factors = _context.factorUW.Get();

            return new ApiResponse<IEnumerable<PurchaseInvoice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = factors
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(int factorId)
        {
            var factor = _context.factorUW.GetById(factorId);

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

        [HttpPost("CreateFactor")]
        public ApiResponse Create([FromBody] FactorCreateModel model)
        {
            if (!ModelState.IsValid)
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
                };

            if (_context.factorUW.Get(f => f.Number == model.FactorNumber).Any())
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name),
                };

            using (var transaction = _context.BeginTransaction())
            {
                try
                {
                    var subtotal = model.purchaseInvoiceItem.Sum(i => i.UnitPrice * i.Count);

                    var invoice = new PurchaseInvoice
                    {
                        Number = model.FactorNumber,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = model.PurchasingUser,
                        TenantId = model.TenantId,
                        SupplierId = model.SupplierId,
                        ProjectId = model.ProjectId,
                        IssueDate = model.IssueDate,
                        IsDeleted = false,
                        Subtotal = subtotal,
                        Tax = model.Tax,
                        Discount = model.Discount,
                        Total = (subtotal + model.Tax) - model.Discount
                    };

                    // افزودن جزئیات به صورت collection
                    foreach (var item in model.purchaseInvoiceItem)
                    {
                        invoice.Items.Add(new PurchaseInvoiceItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Count,
                            UnitPricePurchase = item.UnitPrice,
                            LineTotal = item.UnitPrice * item.Count,
                            TenantId = invoice.TenantId,
                            CreatedById = model.PurchasingUser,
                            CreatedAt = DateTime.UtcNow,
                            IsDeleted = false
                        });
                    }

                    // ذخیره فقط یکبار
                    _context.factorUW.Create(invoice);
                    _context.Save();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Success,
                        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    };
                }
                catch (Exception)
                {
                    transaction.Rollback();

                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.ServerError,
                        Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
                    };
                }
            }
        }

        [HttpPut("UpdateFactor")]
        [ProducesResponseType(typeof(PurchaseInvoice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse Update([FromBody] FactorEditModel model)
        {
            if (model.FactorId == 0 || model.Items == null)
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

            try
            {
                var factor = _context.factorUW.GetById(model.FactorId);

                if (factor == null)
                    return new ApiResponse
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.NotFound,
                        Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                    };

                // به‌روزرسانی اطلاعات فاکتور
                factor.Number = model.FactorNumber;
                factor.PayStatus = model.FactorPayStatus;
                factor.IssueDate = model.FactorDate;


                // حذف جزئیات قبلی با DeleteByRange (بدون لود شدن داده‌ها)
                _context.factorDetailUW.DeleteByRange(d => d.Id == model.FactorId);

                // ساخت جزئیات جدید - ProductRowPrice = Count × UnitPrice
                var newDetails = model.Items.Select(i => new PurchaseInvoiceItem
                {
                    Id = model.FactorId,
                    ProductId = i.ProductId,
                    Quantity = i.Count,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = model.PurchasingUser,
                    TenantId = 1,
                    UnitPricePurchase = i.UnitPrice * i.Count
                }).ToList();

                _context.factorDetailUW.CreateRange(newDetails);

                // محاسبه جمع کل جدید
                factor.Total = newDetails.Sum(x => x.UnitPricePurchase);

                _context.factorUW.Update(factor);
                _context.Save();

                return new ApiResponse<PurchaseInvoice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = factor
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
