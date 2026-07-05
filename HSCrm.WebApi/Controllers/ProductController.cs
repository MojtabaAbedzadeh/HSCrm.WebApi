using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
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
            _dbContext = dbContext;
            _context = context;
            _currentUserService = currentUserService;
        }

        [HttpGet("GetProducts")]
        [Permission(Permissions.Products.View)]
        public ApiResponse<IEnumerable<ProductWithPriceDto>> Get() // خروجی را به DTO تغییر می دهیم
        {
            // 1. Include کردن رابطه
            var productsWithAllPrices = _context.productUW.Get(
                includes: new Expression<Func<Product, object>>[] { p => p.ProductPrices }
            );

            // 2. تبدیل و انتخاب قیمت فعال (اگر بیش از یک قیمت فعال مجاز باشد، اولین مورد انتخاب می شود)
            var resultDto = productsWithAllPrices.Select(p => new ProductWithPriceDto
            {
                
                ProductId = p.Id, 
                ProductTitle = p.ProductTitle,
                IsActive = p.IsActive,
                ProductUnit = p.ProductUnit,
                TenantId = p.TenantId,
                SellPrice = p.ProductPrices
                                   .Where(pp => pp.IsActive && pp.EffectiveTo == null) // یا هر شرط دیگری برای قیمت فعال
                                   .OrderByDescending(pp => pp.EffectiveFrom) // جدیدترین قیمت فعال اول باشد
                                   .Select(pp => pp.SellPrice)
                                   .FirstOrDefault(),
                BuyPrice = p.ProductPrices
                                   .Where(pp => pp.IsActive && pp.EffectiveTo == null) 
                                   .OrderByDescending(pp => pp.EffectiveFrom)
                                   .Select(pp => pp.BuyPrice)
                                   .FirstOrDefault()
            }).ToList();

            return new ApiResponse<IEnumerable<ProductWithPriceDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
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

            if (product == null) return new ApiResponse<Product>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
                Data = product
            };

            return new ApiResponse<Product>

            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = product
            };
        }

        [HttpPost("CreateProduct")]
        [Permission(Permissions.Products.Create)]
        public ApiResponse Create([FromForm] ProductCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getProduct = _context.productUW.Get(b => b.ProductTitle == model.ProductTitle);

            if (getProduct.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Product product = new Product()
                {
                    ProductTitle = model.ProductTitle,
                    ProductUnit = model.ProductUnit,
                    TenantId = User.GetTenantId().Value,
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
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = product
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

        [HttpPut("UpdateProduct")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Products.Edit)]
        public ApiResponse Update([FromBody] ProductEditModel model)
        {
            if (model.ProductId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getProduct = _context.productUW.GetById(model.ProductId);

            if (getProduct != null)
            {
                getProduct.ProductTitle = model.ProductTitle;
                getProduct.ProductUnit = model.ProductUnit;
                getProduct.IsActive = model.IsActive;
                getProduct.ModifiedAt = DateTime.UtcNow;
                getProduct.ModifiedById = User.GetUserId();
            }

            _context.productUW.Update(getProduct);
            _context.Save();

            return new ApiResponse<Product>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getProduct
            };
        }

        [HttpGet("Search")]
        public IActionResult Search([FromQuery] string term)
        {
            Console.WriteLine("Tenant from Controller: " + User.GetTenantId());

            if (string.IsNullOrWhiteSpace(term))
                return Ok(new List<object>());

            term = term.Trim();
            var total = _context.productUW.Get(p => true).Count();

            // 1) دریافت 10 محصول
            var products = _context.productUW.Get(p => p.ProductTitle != null && p.ProductTitle.Contains(term), true)
                .Take(10)
                .ToList();

            // 2) دریافت همه قیمت‌های مرتبط با همین 10 محصول
            var productIds = products.Select(p => p.Id).ToList();

            var prices = _context.priceUW
                .Get(pp => productIds.Contains(pp.ProductId) && pp.IsActive, true)
                .OrderByDescending(pp => pp.EffectiveFrom)
                .ToList();

            // 3) ساخت خروجی بدون Query تو در تو
            var result = products.Select(p => new
            {
                p.Id,
                ProductTitle = p.ProductTitle,

                SellPrice = prices
                    .Where(pp => pp.ProductId == p.Id)
                    .Select(pp => pp.SellPrice)
                    .FirstOrDefault(),

                BuyPrice = prices
                    .Where(pp => pp.ProductId == p.Id)
                    .Select(pp => pp.BuyPrice)
                    .FirstOrDefault()
            }).ToList();
            return Ok(result);
        }
    }
}
