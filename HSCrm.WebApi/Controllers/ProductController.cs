using Common.Permissions;
using Framework.Authorization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;

        public ProductController(IUnitOfWork context, ICurrentUserService currentUserService, ApplicationDbContext dbContext)
        {
            _context = context;
            _currentUserService = currentUserService;
            _dbContext = dbContext;
        }

        [HttpGet("GetProducts")]
        [Permission(Permissions.Products.View)]
        public ApiResponse<IEnumerable<ProductWithPriceDto>> Get()
        {
            var products = _context.productUW.Get(
                includes: new Expression<Func<Product, object>>[] { p => p.ProductPrices }
            );

            var resultDto = products.Select(p => new ProductWithPriceDto
            {
                ProductId = p.Id,
                ProductTitle = p.ProductTitle,
                IsActive = p.IsActive,
                ProductUnit = p.ProductUnit,
                TenantId = p.TenantId,
                SellPrice = p.ProductPrices
                    .Where(pp => pp.IsActive && pp.EffectiveTo == null)
                    .OrderByDescending(pp => pp.EffectiveFrom)
                    .Select(pp => pp.SellPrice)
                    .FirstOrDefault(),
                BuyPrice = p.ProductPrices
                    .Where(pp => pp.IsActive && pp.EffectiveTo == null)
                    .OrderByDescending(pp => pp.EffectiveFrom)
                    .Select(pp => pp.BuyPrice)
                    .FirstOrDefault()
            });

            return new ApiResponse<IEnumerable<ProductWithPriceDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "Success",
                Data = resultDto
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Products.View)]
        public ApiResponse GetById(int productId)
        {
            var product = _context.productUW.GetById(productId);

            if (product == null)
            {
                return new ApiResponse<Product>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "محصول مورد نظر یافت نشد.",
                    Data = null
                };
            }

            return new ApiResponse<Product>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "Success",
                Data = product
            };
        }

        [HttpPost("CreateProduct")]
        [Permission(Permissions.Products.Create)]
        public ApiResponse Create([FromForm] ProductCreateModel model)
        {
            if (model == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "اطلاعات ورودی معتبر نمی باشد."
                };
            }

            if (!ModelState.IsValid)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "اطلاعات فرم ناقص یا نامعتبر است."
                };
            }

            var duplicateProduct = _context.productUW.Get(b => b.ProductTitle == model.ProductTitle).FirstOrDefault();
            if (duplicateProduct != null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.Conflict,
                    Message = "نام محصول تکراری است."
                };
            }

            try
            {
                var product = new Product
                {
                    ProductTitle = model.ProductTitle,
                    ProductUnit = model.ProductUnit,
                    TenantId = User.GetTenantId().GetValueOrDefault(),
                    CreatedById = User.GetUserId(),
                    IsActive = model.IsActive,
                    CreatedAt = DateTime.Now
                };

                _context.productUW.Create(product);
                _context.Save();

                return new ApiResponse<Product>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "محصول جدید با موفقیت ثبت شد.",
                    Data = product
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = $"خطایی در سمت سرور رخ داد: {ex.Message}"
                };
            }
        }

        [HttpPut("UpdateProduct")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Products.Edit)]
        public ApiResponse Update([FromBody] ProductEditModel model)
        {
            if (model == null || model.ProductId == 0)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شناسه محصول نامعتبر است."
                };
            }

            var getProduct = _context.productUW.GetById(model.ProductId);

            if (getProduct == null)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "محصول مورد نظر یافت نشد."
                };
            }

            try
            {
                getProduct.ProductTitle = model.ProductTitle;
                getProduct.ProductUnit = model.ProductUnit;
                getProduct.IsActive = model.IsActive;
                getProduct.ModifiedAt = DateTime.UtcNow;
                getProduct.ModifiedById = User.GetUserId();

                _context.productUW.Update(getProduct);
                _context.Save();

                return new ApiResponse<Product>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "محصول با موفقیت ویرایش شد.",
                    Data = getProduct
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = $"خطایی در ویرایش محصول رخ داد: {ex.Message}"
                };
            }
        }

        [HttpGet("Search")]
        public ApiResponse<IEnumerable<object>> Search([FromQuery] string term)
        {
            if (string.IsNullOrEmpty(term))
            {
                return new ApiResponse<IEnumerable<object>>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "Success",
                    Data = new List<object>()
                };
            }

            var cleanTerm = term.CleanName();

            var products = _context.productUW.Get(p => p.ProductTitle.Contains(cleanTerm))
                .Take(10)
                .ToList();

            var result = new List<object>();

            foreach (var item in products)
            {
                var price = _context.priceUW.Get(p => p.ProductId == item.Id && p.IsActive && p.EffectiveTo == null)
                    .OrderByDescending(p => p.EffectiveFrom)
                    .FirstOrDefault();

                result.Add(new
                {
                    Id = item.Id,
                    ProductTitle = item.ProductTitle,
                    SellPrice = price != null ? price.SellPrice : 0,
                    BuyPrice = price != null ? price.BuyPrice : 0
                });
            }

            return new ApiResponse<IEnumerable<object>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "Success",
                Data = result
            };
        }

        [HttpGet("ProductDropdownList")]
        public ApiResponse ProductDropdownList()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

            var tenantId = User.GetTenantId();

            if (!tenantId.HasValue)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "شناسه شرکت (TenantId) در درخواست موجود نیست."
                };
            }

            var productList = _context.productUW
                .Get(p => p.TenantId == tenantId.Value)
                .Select(p => new DropdownDto
                {
                    DrId = p.Id,
                    DrName = p.ProductTitle
                })
                .ToList();

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = productList
            };
        }
    }
}