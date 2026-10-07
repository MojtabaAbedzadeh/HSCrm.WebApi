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
        private const string DefaultAvatarPath = "/Upload/UserImage/defaultAvatar.png";
        private const string CustomerRoleName = "Customer";

        private readonly IUnitOfWork _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            IUnitOfWork context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<CustomerController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [HttpGet("GetCustomer")]
        [Permission(Permissions.Customers.View)]
        public ApiResponse<IEnumerable<Customer>> Get()
        {
            var customers = _context.customerUW.Get().ToList();

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
        [ProducesResponseType(typeof(CustomerDetailViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Customers.View)]
        public ApiResponse<CustomerDetailViewModel> GetCustomerById(int customerId)
        {
            var customer = _context.customerUW
                .Get(c => c.Id == customerId, false, c => c.ApplicationUser!)
                .FirstOrDefault();

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

            return new ApiResponse<CustomerDetailViewModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = MapToDetailViewModel(customer)
            };
        }

        [HttpGet("GetCustomerProfile")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(CustomerDetailViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Customers.View)]
        public ApiResponse<CustomerDetailViewModel> GetCustomerProfile()
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new ApiResponse<CustomerDetailViewModel>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            var customer = _context.customerUW
                .Get(c => c.ApplicationUserId == userId, false, c => c.ApplicationUser!)
                .FirstOrDefault();

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

            return new ApiResponse<CustomerDetailViewModel>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = MapToDetailViewModel(customer)
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

            if (customer == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                };
            }

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
            {
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var currentUserId = User.GetUserId();
            var tenantId = User.GetTenantId();

            if (string.IsNullOrEmpty(currentUserId) || tenantId == null)
            {
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            if (!await _roleManager.RoleExistsAsync(CustomerRoleName))
            {
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = $"Role '{CustomerRoleName}' در سیستم وجود ندارد."
                };
            }

            var fullName = $"{model.UserFirstName} {model.UserLastName}".Trim();

            var existsCustomer = _context.customerUW
                .Get(c => c.Name == fullName && c.TenantId == tenantId.Value)
                .Any();

            if (existsCustomer)
            {
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var existsUser = await _userManager.FindByNameAsync(model.UserName);
            if (existsUser != null)
            {
                return new ApiResponse<CustomerCreateDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "نام کاربری تکراری است."
                };
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var existsEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existsEmail != null)
                {
                    return new ApiResponse<CustomerCreateDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.Conflict,
                        Message = "ایمیل تکراری است."
                    };
                }
            }

            await using var transaction = await _context.BeginTransactionAsync();

            try
            {
                var user = new ApplicationUser
                {
                    UserFirstName = model.UserFirstName,
                    UserLastName = model.UserLastName,
                    UserName = model.UserName,
                    UserImageUrl = BuildDefaultAvatarUrl(),
                    UserStatus = true,
                    Email = model.Email,
                    PhoneNumber = model.Phone,
                    TenantId = tenantId.Value,
                    UserType = 3,
                    UserGender = model.UserGender,
                    IsTenantOwner = false,
                    EmailConfirmed = false,
                    LockoutEnabled = true
                };

                var createUserResult = await _userManager.CreateAsync(user, model.Password);
                if (!createUserResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return new ApiResponse<CustomerCreateDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = string.Join(" | ", createUserResult.Errors.Select(e => e.Description))
                    };
                }

                var roleResult = await _userManager.AddToRoleAsync(user, CustomerRoleName);
                if (!roleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return new ApiResponse<CustomerCreateDto>
                    {
                        Status = false,
                        StatusCode = ApiResultStatusCode.BadRequest,
                        Message = string.Join(" | ", roleResult.Errors.Select(e => e.Description))
                    };
                }

                var customer = new Customer
                {
                    Name = fullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Address = model.Address,
                    CreatedAt = DateTime.Now,
                    CreatedById = currentUserId,
                    TenantId = tenantId.Value,
                    ApplicationUserId = user.Id
                };

                _context.customerUW.Create(customer);
                await _context.SaveAsync();
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
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating customer for tenant {TenantId}", tenantId);
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
        public async Task<ApiResponse> Update([FromBody] CustomerEditModel model)
        {
            if (model.CustomerId == 0 || string.IsNullOrWhiteSpace(model.Name))
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var currentUserId = User.GetUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var isDuplicateName = _context.customerUW
                .Get(c => c.Name == model.Name && c.Id != model.CustomerId)
                .Any();

            if (isDuplicateName)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var getCustomer = _context.customerUW.GetById(model.CustomerId);
            if (getCustomer == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            getCustomer.Name = model.Name;
            getCustomer.Phone = model.Phone;
            getCustomer.Email = model.Email;
            getCustomer.Address = model.Address;
            getCustomer.ModifiedAt = DateTime.Now;
            getCustomer.ModifiedById = currentUserId;

            if (!string.IsNullOrEmpty(getCustomer.ApplicationUserId))
            {
                var linkedUser = await _userManager.FindByIdAsync(getCustomer.ApplicationUserId);
                if (linkedUser != null)
                {
                    linkedUser.PhoneNumber = model.Phone;
                    linkedUser.Email = model.Email;
                    var updateUserResult = await _userManager.UpdateAsync(linkedUser);
                    if (!updateUserResult.Succeeded)
                    {
                        return new ApiResponse
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = string.Join(" | ", updateUserResult.Errors.Select(e => e.Description))
                        };
                    }
                }
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
        [Permission(Permissions.Customers.View)]
        public ApiResponse CustomerDropdownList()
        {
            var tenantId = User.GetTenantId();
            if (tenantId == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Unauthorized,
                    Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            var customerList = _context.customerUW
                .Get(c => c.TenantId == tenantId.Value)
                .Select(p => new DropdownDto
                {
                    DrId = p.Id,
                    DrName = p.Name,
                })
                .ToList();

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = customerList
            };
        }

        private static CustomerDetailViewModel MapToDetailViewModel(Customer customer)
        {
            var user = customer.ApplicationUser!;

            return new CustomerDetailViewModel
            {
                Id = customer.Id,
                Phone = customer.Phone,
                Email = customer.Email,
                Address = customer.Address,
                UserId = customer.ApplicationUserId!,
                UserFirstName = user.UserFirstName,
                UserLastName = user.UserLastName,
                UserImageUrl = user.UserImageUrl,
                UserStatus = user.UserStatus,
                UserType = user.UserType,
                UserGender = user.UserGender,
                TenantId = customer.TenantId,
                IsTenantOwner = user.IsTenantOwner,
                UserName = user.UserName!
            };
        }

        private string BuildDefaultAvatarUrl()
        {
            return $"{Request.Scheme}://{Request.Host.Value}{DefaultAvatarPath}";
        }
    }
}
