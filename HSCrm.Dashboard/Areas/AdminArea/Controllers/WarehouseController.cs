using HSCrm.BussinessLogic.PublicMethod;
using HSCrm.Dashboard.Controllers;
using HSCrm.Models.Common;
using HSCrm.Models.ModelDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Security.Claims;

namespace HSCrm.Dashboard.Areas.AdminArea.Controllers
{
    [Area(nameof(AdminArea))]
    [Authorize(Roles = "Owner")]
    public class WarehouseController : BaseController
    {
        private readonly IConfiguration _config;
        private readonly GetListApi _getListApi;

        public WarehouseController(IConfiguration config, GetListApi getListApi) : base(config) 
        {
            _config = config;
            _getListApi = getListApi;
        }

        public async Task<IActionResult> Index()
        {
            // از آدرس نسبی استاندارد سازگار با BaseAddress در ApiClient استفاده می‌شود
            string apiUrl = "Warehouse/GetWarehouses";
            string token = User.FindFirstValue("Token");

            try
            {
                var json = await _getListApi.GetApiList(apiUrl);
                if (string.IsNullOrWhiteSpace(json))
                {
                    ViewBag.ErrorMessage = "امکان دریافت لیست انبارها وجود ندارد یا دسترسی به سیستم انبارداری محدود شده است.";
                    return View(new List<WarehousesModel>());
                }

                var result = JsonConvert.DeserializeObject<ApiResponse<List<WarehousesModel>>>(json);
                if (result == null || !result.Status)
                {
                    ViewBag.ErrorMessage = result?.Message ?? "خطا در بازیابی اطلاعات انبارها از سرور.";
                    return View(new List<WarehousesModel>());
                }

                var model = result.Data ?? new List<WarehousesModel>();
                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "خطای غیرمنتظره در برقراری ارتباط با وب‌سرویس: " + ex.Message;
                return View(new List<WarehousesModel>());
            }
        }
    }
}
