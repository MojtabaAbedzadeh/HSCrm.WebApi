using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Entities.Models.Dto;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupplierController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentUserService _currentUserService;

        public SupplierController(IUnitOfWork context, UserManager<ApplicationUser> userManager, ICurrentUserService currentUserService)
        {
            _context = context;
            _userManager = userManager;
            _currentUserService = currentUserService;
        }

        [HttpGet("GetSupplier")]
        [Permission(Permissions.Suppliers.View)]
        public ApiResponse<IEnumerable<Supplier>> Get()
        {
            var suppliers = _context.supplierUW.Get();

            return new ApiResponse<IEnumerable<Supplier>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = suppliers
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Supplier), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Suppliers.View)]
        public ApiResponse GetById(int supplierId)
        {
            var supplier = _context.supplierUW.GetById(supplierId);

            if (supplier == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Supplier>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = supplier
            };
        }

        [HttpPost("CreateSupplier")]
        [Permission(Permissions.Suppliers.Create)]
        public ApiResponse Create([FromForm] SupplierCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getSupplier = _context.supplierUW.Get(b => b.SupplierFullName == model.SupplierFullName);

            if (getSupplier.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Supplier supplier = new Supplier()
                {
                    SupplierFullName = model.SupplierFullName,
                    SupplierAddress = model.SupplierAddress,
                    SupplierPhone = model.SupplierPhone,
                    CreatedById = User.GetUserId(),
                    TenantId = User.GetTenantId().Value,
                    CreatedAt = DateTime.Now,
                };

                _context.supplierUW.Create(supplier);
                _context.Save();

                return new ApiResponse<Supplier>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = supplier
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

        [HttpPut("UpdateSupplier")]
        [ProducesResponseType(typeof(Supplier), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Suppliers.Edit)]
        public ApiResponse Update([FromBody] SupplierEditModel model)
        {
            if (model.SupplierId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getSupplier = _context.supplierUW.GetById(model.SupplierId);

            if (getSupplier != null)
            {
                getSupplier.SupplierFullName = model.SupplierFullName;
                getSupplier.SupplierPhone = model.SupplierPhone;
                getSupplier.SupplierAddress = model.SupplierAddress;
                getSupplier.ModifiedAt = DateTime.Now;
                getSupplier.ModifiedById = User.GetUserId();
            }

            _context.supplierUW.Update(getSupplier);
            _context.Save();

            return new ApiResponse<Supplier>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getSupplier
            };
        }

        [HttpGet("SupplierDropdownList")]
        public ApiResponse SupplierDropdownList(int tenantId)
        {
            IEnumerable<DropdownDto> supplierList = _context.supplierUW.Get(s => s.TenantId == User.GetTenantId().Value).Select(p => new DropdownDto
            {
                DrId = p.Id,
                DrName = p.SupplierFullName,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = supplierList
            };
        }

        [HttpGet("GetFinancialProfile/{supplierId}")]
        [Permission(Permissions.Suppliers.View)]
        public async Task<ApiResponse<SupplierFinancialProfileDto>> GetFinancialProfile(
            int supplierId,
            CancellationToken cancellationToken)
        {
            var tenantId = _currentUserService.TenantId;

            if (tenantId is null)
            {
                return new ApiResponse<SupplierFinancialProfileDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = "Tenant is missing."
                };
            }

            var supplier = await _context.supplierUW.GetByIdAsync(
                supplierId,
                cancellationToken);

            if (supplier == null || supplier.TenantId != tenantId.Value)
            {
                return new ApiResponse<SupplierFinancialProfileDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "تامین‌کننده یافت نشد."
                };
            }

            var invoices = await _context.purchaseInvoiceUW.GetAsync(
                predicate: pi => pi.SupplierId == supplierId
                              && pi.TenantId == tenantId.Value,
                asNoTracking: true,
                ct: cancellationToken
            );

            var payments = await _context.paymentUW.GetAsync(
                predicate: p => p.PurchaseInvoice != null
                             && p.PurchaseInvoice.SupplierId == supplierId
                             && p.TenantId == tenantId.Value,
                asNoTracking: true,
                ct: cancellationToken
            );

            var totalInvoices = invoices.Sum(x => x.Total);
            var totalPayments = payments.Sum(x => x.Amount);

            var profile = new SupplierFinancialProfileDto
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.SupplierFullName,

                TotalInvoices = totalInvoices,
                TotalPayments = totalPayments,

                Invoices = invoices
        .Select(i => new SupplierInvoiceDto
        {
            InvoiceId = i.Id,
            InvoiceNumber = i.Number,
            InvoiceDate = i.IssueDate,
            TotalAmount = i.Total
        })
        .OrderByDescending(i => i.InvoiceDate)
        .ToList(),

                Payments = payments
        .Select(p => new SupplierPaymentDto
        {
            PaymentId = p.Id,
            Amount = p.Amount,
            PaymentDate = p.PaymentDate,
            Method = p.Method,
            ReferenceNo = p.ReferenceNo
        })
        .OrderByDescending(p => p.PaymentDate)
        .ToList()
            };

            return new ApiResponse<SupplierFinancialProfileDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Data = profile
            };
        }

    }
}
