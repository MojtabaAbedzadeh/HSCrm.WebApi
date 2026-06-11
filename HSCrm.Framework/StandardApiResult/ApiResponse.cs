using System.ComponentModel.DataAnnotations;

namespace HSCrm.Framework.StandardApiResult
{
    public class ApiResponse
    {
        public bool Status { get; set; }

        public string Message { get; set; }

        public ApiResultStatusCode StatusCode { get; set; }

        public object AdditionalData { get; set; } 
    }

    public class ApiResponse<Table> : ApiResponse
    {
        public Table Data { get; set; }
    }

    public enum ApiResultStatusCode
    {
        [Display(Name = "عملیات موفق")]
        Success = 200,

        [Display(Name = "لیست خالی می‌باشد!")]
        NoContent = 204,

        [Display(Name = "پارامترهای ارسالی معتبر نیست!")]
        BadRequest = 400,

        [Display(Name = "یافت نشد!")]
        NotFound = 404,

        [Display(Name = "موجودی کافی نیست!")]
        NotEnoughInventory = 406,

        [Display(Name = "اطلاعات تکراری می‌باشد!")]
        Conflict = 409,

        [Display(Name = "خطای تاریخ یا داده نامعتبر!")]
        UnprocessableEntity = 422,

        [Display(Name = "خطا در سرور")]
        ServerError = 500,
    }
}
