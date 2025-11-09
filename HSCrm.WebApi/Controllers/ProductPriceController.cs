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
        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(1200)
        };


        public ProductPriceController(IUnitOfWork context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        //[HttpGet("GetPrices")]
        //public ApiResponse<IEnumerable<ProductPriceList>> Get(PagingModel model)
        //{
           
        //}

        [HttpGet("GetProductPriceHistory")]
        public ApiResponse<IEnumerable<ProductPrice>> GetProductPriceHistory(long ircCode)
        {
            var priceHistory = _context.priceUW.Get(p => p.ProductId == ircCode).OrderByDescending(o => o.Id);

            return new ApiResponse<IEnumerable<ProductPrice>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = priceHistory
            };
        }

        [HttpGet("GetPriceById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ProductPrice), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ApiResponse GetById(long priceId)
        {
            var price = _context.priceUW.GetById(priceId);

            if (price == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<ProductPrice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = price
            };
        }

        [HttpPost]
        [Route("CreatePrice")]
        public ApiResponse Create([FromForm] PriceCreateModel model)
        {
            //کنترل خالی بودن اطلاعات
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            //کنترل تاریخ اعمال هم پوشانی نداشته باشد!
            DateTime ActionDateMiladi = ConvertDate.ConvertShamsiToMiladi(model.EffectiveFrom.ToString());
            var getPrice = _context.priceUW.Get(p => p.ProductId == model.ProductId &&
                                                     p.EffectiveFrom >= ActionDateMiladi);

            var fetchedPrices = _context.priceUW.Get(fp => fp.ProductId == model.ProductId);

            if (getPrice.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            if (fetchedPrices != null)
            {
                foreach (var price in fetchedPrices)
                {
                    price.IsActive = false;
                    _context.priceUW.Update(price);
                }
            }

            try
            {
                ProductPrice price = new ProductPrice()
                {
                    ProductId = model.ProductId,
                    SellPrice = model.SellPrice,
                    IsActive = true,
                    EffectiveFrom = ActionDateMiladi,
                    CreatedAt = DateTime.Now,
                    CreatedById = model.UserId,
                    TenantId = 1
                };
                _context.priceUW.Create(price);
                _context.Save();
                return new ApiResponse<ProductPrice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = price
                };
            }
            catch (Exception)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
        }

        [HttpDelete]
        [Route("DeletePrice")]
        public ApiResponse DeletePrice(long priceId)
        {
            if (priceId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
            };

            var query = _context.priceUW.GetById(priceId);

            if (DateTime.Now.Date < query.EffectiveFrom.Date)
            {
                _context.priceUW.DeleteById(priceId);
                _context.Save();
                return new ApiResponse<ProductPrice>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = query
                };
            }
            else
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name),
                };
            }
        }

        //[HttpGet("UpdatePrices")]
        //public async Task<ApiResponse> UpdatePrices()
        //{
        //    //sinapharm/v1/v1/update/20240918-v1
        //    string apiUrl = "https://bilgisoft.ir/sinapharm/v1/update/20240918-v1.csv"; // URL API را مشخص کنید

        //    var priceUpdates = await ReadPricesFromCsvAsync(apiUrl);

        //    await _priceUpdateService.UpdatePricesAsync(priceUpdates);

        //    return new ApiResponse
        //    {
        //        Status = true,
        //        StatusCode = ApiResultStatusCode.Success,
        //        Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
        //    };
        //}

        //private async Task<List<PriceUpdateModel>> ReadPricesFromCsvAsync(string url)
        //{
        //    try
        //    {
        //        using var response = await _httpClient.GetAsync(url);
        //        response.EnsureSuccessStatusCode();

        //        using var stream = await response.Content.ReadAsStreamAsync();
        //        using var reader = new StreamReader(stream);
        //        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        //        {
        //            HasHeaderRecord = true,
        //        });

        //        var records = csv.GetRecords<PriceUpdateModel>().ToList();
        //        return records;
        //    }
        //    catch (Exception e)
        //    {

        //        throw e;
        //    }
        //}
    }
}
