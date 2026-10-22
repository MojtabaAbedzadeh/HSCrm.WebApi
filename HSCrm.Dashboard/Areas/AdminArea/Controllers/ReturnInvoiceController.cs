using HSCrm.BussinessLogic.PublicMethod;
using HSCrm.Dashboard.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.Dashboard.Areas.AdminArea.Controllers
{
    [Area("AdminArea")]
    public class ReturnInvoiceController : BaseController
    {
        private readonly GetListApi _getListApi;

        public ReturnInvoiceController(IConfiguration config, GetListApi getListApi) : base(config)
        {
            _getListApi = getListApi;
        }

        [HttpGet]
        public IActionResult SalesReturn()
        {
            return View();
        }

        [HttpGet]
        public IActionResult PurchaseReturn()
        {
            return View();
        }
    }
}
