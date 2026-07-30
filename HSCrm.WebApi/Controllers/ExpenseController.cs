using Common.Permissions;
using Framework.Authorization;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExpenseController : ControllerBase
    {
        private readonly IUnitOfWork _context;
        private readonly IPaymentSettlementService _paymentSettlementService;


        public ExpenseController(IUnitOfWork context, IPaymentSettlementService paymentSettlementService)
        {
            _context = context;
            _paymentSettlementService = paymentSettlementService;
        }

        [HttpGet("SettlementSummary/{id}")]
        public IActionResult SettlementSummary(int id)
        {
            var expense = _context.expenseUW.GetById(id); // اگر متد/ریپازیتوری‌ات فرق دارد، همان را بگذار
            if (expense == null)
                return NotFound();

            var totalAmount = expense.Amount; // یا مبلغ اصلی هزینه
            var result = _paymentSettlementService.BuildExpenseSummary(id, totalAmount);
            return Ok(result);
        }


        [HttpGet("GetExpense")]
        [Permission(Permissions.Expenses.View)]
        public ApiResponse<IEnumerable<Expense>> Get()
        {
            var expenses = _context.expenseUW.Get(null, true, x => x.Project, x => x.Tenant, x => x.CreatedBy).ToList();

            return new ApiResponse<IEnumerable<Expense>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = expenses
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Expenses.View)]
        public ApiResponse GetById(int expenseId)
        {
            var expenses = _context.expenseUW
                .Get(e => e.Id == expenseId)
                .Select(e => new ExpenseEditModel
                {
                    ExpenseId = e.Id,
                    ExpenseTitle = e.ExpenseTitle,
                    ProjectId = e.ProjectId,
                    Type = e.Type,
                    Amount = e.Amount,
                    Description = e.Description,
                    ExpenseDate = ConvertDate.ConvertMiladiToShamsi(e.ExpenseDate, "yyyy/MM/dd")
                })
                .ToList();

            if (!expenses.Any())
                return new ApiResponse<IEnumerable<ExpenseEditModel>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "هزینه‌ای برای این پروژه ثبت نشده است"
                };

            return new ApiResponse<IEnumerable<ExpenseEditModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = expenses
            };
        }

        [HttpGet("GetByProjectId")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Expenses.View)]
        public ApiResponse<IEnumerable<ExpenseEditModel>> GetByProjectId(int projectId)
        {
            var expenses = _context.expenseUW
                .Get(e => e.ProjectId == projectId)
                .Select(e => new ExpenseEditModel
                {
                    ExpenseId = e.Id,
                    ExpenseTitle = e.ExpenseTitle,
                    ProjectId = e.ProjectId,
                    Type = e.Type,
                    Amount = e.Amount,
                    Description = e.Description,
                    ExpenseDate = ConvertDate.ConvertMiladiToShamsi(e.ExpenseDate, "yyyy/MM/dd")
                }).ToList();

            if (!expenses.Any())
                return new ApiResponse<IEnumerable<ExpenseEditModel>>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.NotFound,
                    Message = "هزینه‌ای برای این پروژه ثبت نشده است"
                };

            return new ApiResponse<IEnumerable<ExpenseEditModel>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = expenses
            };
        }

        [HttpPost("CreateExpense")]
        [Permission(Permissions.Expenses.Create)]
        public ApiResponse Create([FromForm] ExpenseCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getExpense = _context.expenseUW.Get(c => c.ExpenseTitle == model.ExpenseTitle);

            if (getExpense.Count() > 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Expense expense = new Expense()
                {
                    TenantId = User.GetTenantId().Value,
                    ExpenseTitle = model.ExpenseTitle,
                    ProjectId = model.ProjectId,
                    Type = model.Type,
                    ExpenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate),
                    Amount = model.Amount,
                    Description = model.Description,
                    CreatedAt = DateTime.Now,
                    CreatedById = User.GetUserId()
                };

                _context.expenseUW.Create(expense);
                _context.Save();

                return new ApiResponse<Expense>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = expense
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

        [HttpPut("UpdateExpense")]
        [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Expenses.Edit)]
        public ApiResponse Update([FromBody] ExpenseEditModel model)
        {
            if (model.ExpenseId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getExpense = _context.expenseUW.GetById(model.ExpenseId);

            if (getExpense != null)
            {
                getExpense.ExpenseTitle = model.ExpenseTitle;
                getExpense.Description = model.Description;
                getExpense.Type = model.Type;
                getExpense.Amount = model.Amount;
                getExpense.ModifiedAt = DateTime.UtcNow;
                getExpense.ModifiedById = User.GetUserId();
                getExpense.ExpenseDate = ConvertDate.ConvertShamsiToMiladi(model.ExpenseDate);
            }

            _context.expenseUW.Update(getExpense);
            _context.Save();

            return new ApiResponse<Expense>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getExpense
            };
        }
    }
}
