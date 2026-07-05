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
using CustomerCreateModel = HSCrm.Entities.Models.CustomerCreateModel;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public CustomerController(IUnitOfWork context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("GetCustomer")]
        [Permission(Permissions.Customers.View)]
        public ApiResponse<IEnumerable<Customer>> Get()
        {
            var customers = _context.customerUW.Get();

            return new ApiResponse<IEnumerable<Customer>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = customers
            };
        }

        [HttpGet("GetCustomerById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Customers.View)]
        public ApiResponse<CustomerDetailViewModel> GetCustomerById(int customerId)
        {
            var customer = _context.customerUW.Get(null, false, c => c.ApplicationUser).FirstOrDefault(c => c.Id == customerId);

            if (customer == null)
            {
                return new ApiResponse<CustomerDetailViewModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            var viewModel = new CustomerDetailViewModel
            {
                Id = customer.Id,
                Phone = customer.Phone,
                Email = customer.Email,
                Address = customer.Address,
                UserId = customer.ApplicationUserId,
                UserFirstName = customer.ApplicationUser!.UserFirstName,
                UserLastName = customer.ApplicationUser.UserLastName,
                UserImageUrl = customer.ApplicationUser.UserImageUrl,
                UserStatus = customer.ApplicationUser.UserStatus,
                UserType = customer.ApplicationUser.UserType,
                UserGender = customer.ApplicationUser.UserGender,
                TenantId = customer.TenantId,
                IsTenantOwner = customer.ApplicationUser.IsTenantOwner,
                UserName = customer.ApplicationUser.UserName
            };

            return new ApiResponse<CustomerDetailViewModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = viewModel
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Customers.View)]
        public ApiResponse GetById(int customerId)
        {
            var customer = _context.customerUW.GetById(customerId);

            if (customer == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Customer>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = customer
            };
        }

        [HttpPost("CreateCustomer")]
        [Permission(Permissions.Customers.Create)]
        public async Task<ApiResponse<CustomerCreateDto>> Create([FromForm] CustomerCreateModel model)
        {
            if (!ModelState.IsValid)
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };

            var currentUserId = User.GetUserId();
            var tenantId = User.GetTenantId();

            if (string.IsNullOrEmpty(currentUserId))
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };

            if (tenantId == null)
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = "شناسه شرکت وارد شده، معتبر نیست"
                };

            var existsCustomer = _context.customerUW
                .Get(c => c.Name == model.UserFirstName + " " + model.UserLastName && c.TenantId == tenantId.Value)
                .Any();

            if (existsCustomer)
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                };

            var existsUser = await _userManager.FindByNameAsync(model.UserName);

            if (existsUser != null)
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                };

            using var transaction = _context.BeginTransaction();

            try
            {
                var user = new ApplicationUser
                {
                    UserFirstName = model.UserFirstName,
                    UserLastName = model.UserLastName,
                    UserName = model.UserName,
                    UserImageUrl = "https://localhost:7096/Upload/UserImage/defaultAvatar.png",
                    UserStatus = true,
                    Email = model.Email,
                    PhoneNumber = model.Phone,
                    TenantId = tenantId.Value,
                    UserType = 3,
                    UserGender = true,
                    IsTenantOwner = false
                };

                var createUserResult = await _userManager.CreateAsync(user, model.Password);

                if (!createUserResult.Succeeded)
                    return new ApiResponse<CustomerCreateDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = string.Join(" | ", createUserResult.Errors.Select(e => e.Description))
                    };

                await _userManager.AddToRoleAsync(user, "Customer");

                var customer = new Customer
                {
                    Name = model.UserFirstName + " " + model.UserLastName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = model.Address,
                    CreatedAt = DateTime.Now,
                    CreatedById = currentUserId,
                    TenantId = tenantId.Value,
                    ApplicationUserId = user.Id
                };

                _context.customerUW.Create(customer);
                _context.Save();

                await transaction.CommitAsync();

                var result = new CustomerCreateDto
                {
                    Id = customer.Id,
                    Name = customer.Name,
                    Phone = customer.Phone,
                    Email = customer.Email,
                    Address = customer.Address,
                    TenantId = customer.TenantId,
                    ApplicationUserId = customer.ApplicationUserId
                };

                return new ApiResponse<CustomerCreateDto>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = result
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPut("UpdateCustomer")]
        [ProducesResponseType(typeof(Customer), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Customers.Edit)]
        public ApiResponse Update([FromBody] CustomerEditModel model)
        {
            if (model.CustomerId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var isDuplicateName = _context.customerUW.Get().Any(c => c.Name == model.Name && c.Id != model.CustomerId && !c.IsDeleted);

            if (isDuplicateName) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getCustomer = _context.customerUW.GetById(model.CustomerId);

            if (getCustomer != null)
            {
                getCustomer.Name = model.Name;
                getCustomer.Phone = model.Phone;
                getCustomer.Email = model.Email;
                getCustomer.Address = model.Address;
                getCustomer.ModifiedAt = DateTime.Now;
                getCustomer.ModifiedById = model.ModifiedById;
            }

            _context.customerUW.Update(getCustomer);
            _context.Save();

            return new ApiResponse<Customer>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getCustomer
            };
        }

        [HttpGet("CustomerDropdownList")]
        public ApiResponse CustomerDropdownList(int tenantId)
        {
            IEnumerable<DropdownDto> customerList = _context.customerUW.Get(c => c.TenantId == User.GetTenantId().Value).Select(p => new DropdownDto
            {
                DrId = p.Id,
                DrName = p.Name,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = customerList
            };
        }
    }
}
