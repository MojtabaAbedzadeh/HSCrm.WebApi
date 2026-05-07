using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.StringCleaner;
using static HSCrm.Entities.Models.PriceModel;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductPriceController : ControllerBase
    {
        private readonly IUnitOfWork _context;

        public ProductPriceController(IUnitOfWork context)
        {
            _context = context;
        }

        [HttpGet("GetProductPriceHistory")]
        public ApiResponse<IEnumerable<ProductPrice>> GetProductPriceHistory(long productId)
        {
            var priceHistory = _context.priceUW.Get(p => p.ProductId == productId).OrderByDescending(o => o.Id);

            return new ApiResponse<IEnumerable<ProductPrice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = priceHistory
            };
        }

        [HttpGet("GetPriceByProductId")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ProductPrice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse<IEnumerable<ProductPrice>> GetPriceByProductId(int productId)
        {
            var price = _context.priceUW.Get(p => p.ProductId == productId);

            if (price == null)
            {
                return new ApiResponse<IEnumerable<ProductPrice>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = ApiResultStatusCode.NotFound
                        .DisplayNameAttribute(DisplayProperty.Name),
                    Data = null
                };
            }

            return new ApiResponse<IEnumerable<ProductPrice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success
                    .DisplayNameAttribute(DisplayProperty.Name),
                Data = price
            };
        }

        [HttpPost("CreatePrice")]
        public ApiResponse Create([FromForm] PriceCreateModel model)
        {
            if (!ModelState.IsValid)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
                };
            }

            // گرفتن قیمت فعال قبلی
            var oldPrice = _context.priceUW
                .Get(p => p.ProductId == model.ProId && p.EffectiveTo == null)
                .FirstOrDefault();

            // اگر قیمت فعال قبلی وجود دارد -> ببندش
            if (oldPrice != null)
            {
                oldPrice.EffectiveTo = DateTime.Now;
                oldPrice.IsActive = false;
                oldPrice.ModifiedAt = DateTime.Now;
                oldPrice.ModifiedById = model.UserId;

                _context.priceUW.Update(oldPrice);
                _context.Save(); // مهم
            }

            try
            {
                ProductPrice newPrice = new ProductPrice()
                {
                    ProductId = model.ProId,
                    SellPrice = model.SellPrice,
                    BuyPrice = model.BuyPrice,
                    IsActive = true,
                    EffectiveFrom = ConvertDate.ConvertShamsiToMiladi(model.EffectiveFrom),
                    EffectiveTo = null,
                    CreatedAt = DateTime.Now,
                    CreatedById = model.UserId,
                    IsDeleted = false,
                    TenantId = model.TenantId
                };

                _context.priceUW.Create(newPrice);
                _context.Save();

                return new ApiResponse<ProductPrice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = newPrice
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

        [HttpPut("UpdatePrice")]
        [ProducesResponseType(typeof(ProductPrice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse Update([FromForm] PriceEditModel model)
        {
            // اعتبارسنجی اولیه
            if (model.ProductId == 0)
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "Invalid ProductId"
                };

            // پیدا کردن قیمت فعال فعلی
            var currentPrice = _context.priceUW
                .Get(p => p.ProductId == model.ProductId && p.EffectiveTo == null)
                .FirstOrDefault();

            if (currentPrice == null)
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "No active price found for this product"
                };

            // اگر قیمت تغییر نکرده است، هیچ کاری انجام نده
            if (currentPrice.SellPrice == model.SellPrice)
            {
                return new ApiResponse<ProductPrice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "Price is already up to date",
                    Data = currentPrice
                };
            }

            try
            {
                // 1️⃣ بروزرسانی قیمت فعلی: پایان آن با تاریخ جاری
                currentPrice.EffectiveTo = DateTime.UtcNow;
                currentPrice.IsActive = false;
                currentPrice.ModifiedAt = DateTime.UtcNow;
                currentPrice.ModifiedById = model.UserId;
                _context.priceUW.Update(currentPrice);

                // 2️⃣ ایجاد رکورد جدید قیمت
                var newPrice = new ProductPrice
                {
                    ProductId = model.ProductId,
                    SellPrice = model.SellPrice,
                    EffectiveFrom = DateTime.UtcNow,
                    EffectiveTo = null,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = model.UserId,
                    TenantId = model.TenantId,
                    IsDeleted = false
                };

                _context.priceUW.Create(newPrice);

                // 3️⃣ ذخیره تغییرات یکجا
                _context.Save();

                return new ApiResponse<ProductPrice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = "Price updated successfully",
                    Data = newPrice
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = $"Server error: {ex.Message}"
                };
            }
        }
    }
}
