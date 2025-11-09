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
    public class ProductController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public ProductController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetProducts")]
        public ApiResponse<IEnumerable<Product>> Get()
        {
            var products = _context.productUW.Get();

            return new ApiResponse<IEnumerable<Product>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = products
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
                    CreatedAt = DateTime.Now,
                    CreatedById = model.UserId,
                    TenantId = model.TenantId
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
        public ApiResponse Update([FromForm] ProductEditModel model)
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
                getProduct.ModifiedAt = DateTime.UtcNow;
                getProduct.ModifiedById = model.UserId;
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
    }
}
