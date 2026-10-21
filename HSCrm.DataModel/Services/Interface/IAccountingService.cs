using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IAccountingService
    {
        /// <summary>
        /// ایجاد کدینگ پیش‌فرض استاندارد برای شرکت (Tenant) در صورت عدم وجود
        /// </summary>
        Task EnsureDefaultChartOfAccountsAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// ثبت سند حسابداری دستی با کنترل کامل تراز بودن
        /// </summary>
        Task<JournalVoucher> CreateManualVoucherAsync(JournalVoucherCreateModel model, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری هنگام نهایی‌سازی فاکتور فروش (SalesInvoice)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForSalesInvoiceAsync(SalesInvoice invoice, InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری هنگام نهایی‌سازی فاکتور خرید (PurchaseInvoice)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForPurchaseInvoiceAsync(PurchaseInvoice invoice, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری هنگام ثبت دریافت خزانه (Receipt)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForReceiptAsync(Receipt receipt, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری هنگام ثبت پرداخت خزانه (Payment)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForPaymentAsync(Payment payment, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند شناسایی هزینه پروژه (Expense)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForExpenseAsync(Expense expense, CancellationToken ct = default);

        /// <summary>
        /// ابطال سند مرتبط با یک مرجع (مثلاً هنگام Cancel یا Reject شدن فاکتور فروش یا خرید)
        /// </summary>
        Task CancelVoucherByReferenceAsync(AccountingReferenceType referenceType, int referenceId, string? reason = null, CancellationToken ct = default);

        /// <summary>
        /// گزارش تراز آزمایشی (Trial Balance) در سال مالی جاری
        /// </summary>
        Task<List<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);

        /// <summary>
        /// گزارش گردش حساب (دفتر معین یا تفصیلی مشتری/تأمین‌کننده/پروژه) همراه با مانده خطی
        /// </summary>
        Task<List<LedgerTransactionDto>> GetLedgerAsync(
            int? accountId = null,
            int? customerId = null,
            int? supplierId = null,
            int? projectId = null,
            int? bankAccountId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند موجودی اول دوره هنگام تعریف حساب بانکی / صندوق جدید
        /// </summary>
        Task<JournalVoucher?> CreateOpeningVoucherForBankAccountAsync(BankAccount bankAccount, CancellationToken ct = default);

        /// <summary>
        /// گزارش خلاصه موجودی لحظه‌ای، کل واریزی‌ها و کل برداشت‌های هر حساب بانکی / صندوق
        /// </summary>
        Task<List<BankAccountBalanceSummaryDto>> GetBankAccountsBalanceSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);

        /// <summary>
        /// گزارش صورت سود و زیان (Profit & Loss)
        /// </summary>
        Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);

        /// <summary>
        /// بستن حساب‌های موقت (درآمد و هزینه)، انتقال سود و زیان به سرمایه/سود انباشته،
        /// صدور سند اختتامیه در سال مالی جاری و صدور خودکار سند افتتاحیه در سال مالی جدید
        /// </summary>
        Task<CloseFiscalYearResultDto> CloseFiscalYearAndGenerateOpeningAsync(CloseFiscalYearRequestModel model, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری هنگام ثبت اولیه چک دریافتنی (از مشتری) یا چک پرداختنی (به تأمین‌کننده)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForChequeRegistrationAsync(Cheque cheque, CancellationToken ct = default);

        /// <summary>
        /// تغییر وضعیت چک (واگذاری به بانک، وصول/پاس شدن، برگشت چک، خرج چک به تأمین‌کننده) و صدور خودکار سند حسابداری مرحله‌ای
        /// </summary>
        Task<JournalVoucher> ChangeChequeStatusAndCreateVoucherAsync(ChequeStatusChangeModel model, CancellationToken ct = default);

        /// <summary>
        /// موتور محاسبه راس‌گیری چک‌های دریافتنی و پرداختنی (میانگین موزون زمانی و ریالی سررسید چک‌ها + تاریخ راس دقیق به شمسی)
        /// </summary>
        Task<ChequeRasGiriResultDto> CalculateChequesRasGiriAsync(ChequeRasGiriRequestModel model, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری دوبل برای فاکتور برگشت از فروش (شامل معکوس کردن درآمد فروش، کاهش بدهی مشتری، ورود مجدد به انبار 1105 و کاهش بهای تمام‌شده 5101)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForSalesReturnAsync(SalesReturnInvoice returnInvoice, CancellationToken ct = default);

        /// <summary>
        /// صدور خودکار سند حسابداری دوبل برای فاکتور برگشت از خرید (کاهش بدهی به تأمین‌کننده 2101 و کسر از موجودی انبار 1105)
        /// </summary>
        Task<JournalVoucher> CreateVoucherForPurchaseReturnAsync(PurchaseReturnInvoice returnInvoice, CancellationToken ct = default);
    }
}