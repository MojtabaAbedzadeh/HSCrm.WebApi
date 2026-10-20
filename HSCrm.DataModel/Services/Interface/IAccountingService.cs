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
        Task<JournalVoucher> CreateVoucherForSalesInvoiceAsync(SalesInvoice invoice, CancellationToken ct = default);

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
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default);

        /// <summary>
        /// گزارش صورت سود و زیان (Profit & Loss)
        /// </summary>
        Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);

        /// <summary>
        /// بستن حساب‌های موقت (درآمد و هزینه)، انتقال سود و زیان به سرمایه/سود انباشته،
        /// صدور سند اختتامیه در سال مالی جاری و صدور خودکار سند افتتاحیه در سال مالی جدید
        /// </summary>
        Task<CloseFiscalYearResultDto> CloseFiscalYearAndGenerateOpeningAsync(CloseFiscalYearRequestModel model, CancellationToken ct = default);
    }
}