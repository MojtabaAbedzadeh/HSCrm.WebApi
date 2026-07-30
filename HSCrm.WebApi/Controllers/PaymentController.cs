using AutoMapper;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IMapper _mapper;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        private readonly ICurrentUserService _currentUserService;

        public PaymentController(
            IUnitOfWork context,
            IMapper mapper,
            ICurrentFiscalYearService currentFiscalYearService,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _mapper = mapper;
            _currentFiscalYearService = currentFiscalYearService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        [Permission(Permissions.Payment.View)]
        public async Task<ActionResult<ApiResponse<IEnumerable<PaymentModel>>>> GetAll(CancellationToken cancellationToken)
        {
            var payments = await _context.paymentUW.GetAsync(
    x => x.TenantId == _currentUserService.TenantId
         && x.FiscalYearId == _currentFiscalYearService.FiscalYearId,
    true,
    cancellationToken,
    x => x.PurchaseInvoice!,
    x => x.Expense!);

            var result = _mapper.Map<IEnumerable<PaymentModel>>(payments);

            return Ok(new ApiResponse<IEnumerable<PaymentModel>>
            {
                Status = true,
                Data = result
            });
        }

        [HttpGet("{id:int}")]
        [Permission(Permissions.Payment.View)]
        public async Task<ActionResult<ApiResponse<PaymentModel>>> GetById(int id, CancellationToken cancellationToken)
        {
            var payment = await _context.paymentUW.GetByIdAsync(id, cancellationToken);

            if (payment == null || payment.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<PaymentModel>
                {
                    Status = false,
                    Message = "Payment not found"
                });
            }

            var result = _mapper.Map<PaymentModel>(payment);

            return Ok(new ApiResponse<PaymentModel>
            {
                Status = true,
                Data = result
            });
        }

        [HttpPost]
        [Permission(Permissions.Payment.Create)]
        public async Task<ActionResult<ApiResponse<PaymentModel>>> Create([FromBody] PaymentCreateModel model, CancellationToken cancellationToken)
        {
            var payment = _mapper.Map<Payment>(model);

            payment.TenantId = _currentUserService.TenantId ?? model.TenantId;
            payment.FiscalYearId = (int)_currentFiscalYearService.FiscalYearId;
            payment.CreatedAt = DateTime.Now;
            payment.CreatedById = model.UserId;

            await _context.paymentUW.CreateAsync(payment, cancellationToken);
            await _context.SaveAsync();

            var result = _mapper.Map<PaymentModel>(payment);

            return Ok(new ApiResponse<PaymentModel>
            {
                Status = true,
                Data = result,
                Message = "Payment created successfully"
            });
        }

        [HttpPut("{id:int}")]
        [Permission(Permissions.Payment.Edit)]
        public async Task<ActionResult<ApiResponse<PaymentModel>>> Update(int id, [FromBody] PaymentEditModel model, CancellationToken cancellationToken)
        {
            var payment = await _context.paymentUW.GetByIdAsync(id, cancellationToken);

            if (payment == null || payment.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<PaymentModel>
                {
                    Status = false,
                    Message = "Payment not found"
                });
            }

            payment.PurchaseInvoiceId = model.PurchaseInvoiceId;
            payment.ExpenseId = model.ExpenseId;
            payment.PaymentDate = model.PaymentDate;
            payment.Amount = model.Amount;
            payment.Method = model.Method;
            payment.ReferenceNo = model.ReferenceNo;
            payment.Description = model.Description;
            payment.ModifiedAt = DateTime.Now;
            payment.ModifiedById = _currentUserService.UserId;

            _context.paymentUW.Update(payment, cancellationToken);
            await _context.SaveAsync();

            var result = _mapper.Map<PaymentModel>(payment);

            return Ok(new ApiResponse<PaymentModel>
            {
                Status = true,
                Data = result,
                Message = "Payment updated successfully"
            });
        }

        [HttpDelete("{id:int}")]
        [Permission(Permissions.Payment.Delete)]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(int id, CancellationToken cancellationToken)
        {
            var payment = await _context.paymentUW.GetByIdAsync(id, cancellationToken);

            if (payment == null || payment.TenantId != _currentUserService.TenantId)
            {
                return NotFound(new ApiResponse<bool>
                {
                    Status = false,
                    Message = "Payment not found"
                });
            }

            _context.paymentUW.Delete(payment, cancellationToken);
            await _context.SaveAsync();

            return Ok(new ApiResponse<bool>
            {
                Status = true,
                Data = true,
                Message = "Payment deleted successfully"
            });
        }
    }
}
