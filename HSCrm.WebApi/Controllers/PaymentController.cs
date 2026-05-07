using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public PaymentController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetPayments")]
        public ApiResponse<IEnumerable<Payment>> Get()
        {
            var payments = _context.paymentUW.Get();

            return new ApiResponse<IEnumerable<Payment>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = payments
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Payment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(int paymentId)
        {
            var payment = _context.paymentUW.GetById(paymentId);

            if (payment == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Payment>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = payment
            };
        }

        [HttpPost("CreatePayment")]
        public ApiResponse Create([FromForm] PaymentCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Payment payment = new Payment()
                {
                    Method = model.Method,
                    IsDeleted = false,
                    PaymentDate = DateTime.Now,
                    PurchaseInvoiceId = model.PurchaseInvoiceId,
                    ReferenceNo = model.ReferenceNo,
                    SalesInvoiceId = model.SalesInvoiceId,
                    ExpenseId = model.ExpenseId,
                    CreatedAt = DateTime.Now,
                    CreatedById = model.UserId,
                    TenantId = model.TenantId,
                };

                _context.paymentUW.Create(payment);
                _context.Save();

                return new ApiResponse<Payment>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = payment
                };
            }
            catch (Exception)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPut("UpdatePayment")]
        [ProducesResponseType(typeof(Payment), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse Update([FromForm] PaymentEditModel model)
        {
            if (model.PaymentId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getPayment = _context.paymentUW.GetById(model.PaymentId);

            if (getPayment != null)
            {
                getPayment.SalesInvoiceId = model.SalesInvoiceId;
                getPayment.PurchaseInvoiceId = model.PurchaseInvoiceId;
                getPayment.ExpenseId = model.ExpenseId;
                getPayment.PaymentDate = model.PaymentDate;
                getPayment.Amount = model.Amount;
                getPayment.Method = model.Method;
                getPayment.ReferenceNo = model.ReferenceNo;
                getPayment.ModifiedAt = DateTime.Now;
                getPayment.ModifiedById = model.UserId;
            }

            _context.paymentUW.Update(getPayment);
            _context.Save();

            return new ApiResponse<Payment>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getPayment
            };
        }
    }
}
