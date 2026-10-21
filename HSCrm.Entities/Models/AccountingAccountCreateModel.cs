using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class AccountingAccountCreateModel
    {
        public required string Code { get; set; }
        public required string Title { get; set; }
        public AccountLevel Level { get; set; }
        public AccountCategory Category { get; set; }
        public AccountNature Nature { get; set; }
        public int? ParentId { get; set; }
        public string? Description { get; set; }
    }

    public class JournalVoucherCreateModel
    {
        /// <summary>
        /// تاریخ شمسی به فرمت 1403/07/25
        /// </summary>
        public required string VoucherDateShamsi { get; set; }
        public required string Description { get; set; }
        public bool PostImmediately { get; set; } = true;
        public List<JournalVoucherItemCreateModel> Items { get; set; } = new();
    }

    public class JournalVoucherItemCreateModel
    {
        public int AccountId { get; set; }
        public required string Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public int? ProjectId { get; set; }
        public int? WarehouseId { get; set; }
        public int? BankAccountId { get; set; }
    }

    /// <summary>
    /// خروجی تراز آزمایشی (Trial Balance)
    /// </summary>
    public class TrialBalanceRowDto
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = default!;
        public string AccountTitle { get; set; } = default!;
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }
        public string CategoryTitle { get; set; } = default!;
        public decimal TurnoverDebit { get; set; }
        public decimal TurnoverCredit { get; set; }
        public decimal BalanceDebit => TurnoverDebit > TurnoverCredit ? TurnoverDebit - TurnoverCredit : 0m;
        public decimal BalanceCredit => TurnoverCredit > TurnoverDebit ? TurnoverCredit - TurnoverDebit : 0m;
    }

    /// <summary>
    /// خروجی گردش حساب (دفتر معین / تفصیلی با مانده در گردش)
    /// </summary>
    public class LedgerTransactionDto
    {
        public int VoucherId { get; set; }
        public int VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        public string AccountCode { get; set; } = default!;
        public string AccountTitle { get; set; } = default!;
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }
        public string Description { get; set; } = default!;
        public string ReferenceTypeTitle { get; set; } = default!;
        public int? ReferenceId { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public string BalanceNature => RunningBalance > 0 ? "بد" : RunningBalance < 0 ? "بس" : "بی‌حساب";
    }

    /// <summary>
    /// خلاصه صورت سود و زیان (Income Statement / P&L)
    /// </summary>
    public class ProfitAndLossReportDto
    {
        public decimal TotalSalesRevenue { get; set; }
        public decimal TotalPurchasesAndCost { get; set; }
        public decimal GrossProfit => TotalSalesRevenue - TotalPurchasesAndCost;
        public decimal TotalOperatingExpenses { get; set; }
        public decimal NetProfit => GrossProfit - TotalOperatingExpenses;
        public List<TrialBalanceRowDto> RevenueAccounts { get; set; } = new();
        public List<TrialBalanceRowDto> ExpenseAccounts { get; set; } = new();
    }

    /// <summary>
    /// مدل تعریف یا ویرایش سال مالی
    /// </summary>
    public class FiscalYearCreateModel
    {
        public required string Title { get; set; }
        public required string StartDateShamsi { get; set; }
        public required string EndDateShamsi { get; set; }
        public bool IsCurrent { get; set; } = false;
    }

    /// <summary>
    /// ورودی عملیات بستن سال مالی و صدور خودکار سند اختتامیه و افتتاحیه
    /// </summary>
    public class CloseFiscalYearRequestModel
    {
        /// <summary>
        /// شناسه سال مالی که قصد بستن آن را داریم (در صورت خالی بودن، سال مالی جاری بسته می‌شود)
        /// </summary>
        public int? SourceFiscalYearId { get; set; }

        /// <summary>
        /// عنوان سال مالی جدید برای انتقال مانده‌ها (مثال: 1405). اگر وجود نداشته باشد خودکار ساخته می‌شود
        /// </summary>
        public required string NextFiscalYearTitle { get; set; }

        /// <summary>
        /// تاریخ شروع سال مالی جدید به شمسی (مثال: 1405/01/01)
        /// </summary>
        public required string NextStartDateShamsi { get; set; }

        /// <summary>
        /// تاریخ پایان سال مالی جدید به شمسی (مثال: 1405/12/29)
        /// </summary>
        public required string NextEndDateShamsi { get; set; }

        /// <summary>
        /// آیا در صورت وجود اسناد پیش‌نویس (Draft) در سال مالی مبدأ، قبل از بستن سال به‌طور خودکار قطعی شوند؟
        /// </summary>
        public bool AutoPostDraftVouchers { get; set; } = false;
    }

    /// <summary>
    /// جزئیات هر حساب در پیش‌نمایش عملیات پایان سال مالی (حساب‌های موقت یا دائم)
    /// </summary>
    public class ClosingAccountPreviewItemDto
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = default!;
        public string AccountTitle { get; set; } = default!;
        public string CategoryTitle { get; set; } = default!;
        public decimal BalanceDebit { get; set; }
        public decimal BalanceCredit { get; set; }
        public string? SubAccountSummary { get; set; }
        public string ClosingActionDescription { get; set; } = default!;
    }

    /// <summary>
    /// پیش‌نمایش و ممیزی کنترلی قبل از بستن سال مالی (Pre-Closing Audit & Preview)
    /// </summary>
    public class FiscalYearClosingPreviewDto
    {
        public int FiscalYearId { get; set; }
        public string FiscalYearTitle { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
        public bool CanClose { get; set; }
        public int UnpostedDraftVouchersCount { get; set; }
        public int TotalPostedVouchersCount { get; set; }
        public decimal TotalRevenues { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfitOrLoss { get; set; }
        public bool IsProfitable => NetProfitOrLoss >= 0;
        public List<string> ValidationMessages { get; set; } = new();
        public List<ClosingAccountPreviewItemDto> TemporaryAccountsToClose { get; set; } = new();
        public List<ClosingAccountPreviewItemDto> PermanentAccountsToTransfer { get; set; } = new();
    }

    /// <summary>
    /// خروجی عملیات بستن سال مالی (شماره اسناد صادرشده و سود/زیان منتقل‌شده)
    /// </summary>
    public class CloseFiscalYearResultDto
    {
        public int ClosedFiscalYearId { get; set; }
        public string ClosedFiscalYearTitle { get; set; } = default!;
        public int NewFiscalYearId { get; set; }
        public string NewFiscalYearTitle { get; set; } = default!;
        public decimal NetProfitOrLossTransferred { get; set; }
        public int TemporaryAccountsClosedCount { get; set; }
        public int PermanentLinesTransferredCount { get; set; }
        public int? ProfitLossClosingVoucherId { get; set; }
        public int? ProfitLossClosingVoucherNumber { get; set; }
        public int? ClosingVoucherId { get; set; }
        public int? ClosingVoucherNumber { get; set; }
        public int? OpeningVoucherIdInNewYear { get; set; }
        public int? OpeningVoucherNumberInNewYear { get; set; }
        public decimal TotalPermanentBalanceTransferred { get; set; }
    }

    /// <summary>
    /// خروجی عملیات بازگشایی سال مالی بسته شده جهت انجام اصلاحات
    /// </summary>
    public class ReopenFiscalYearResultDto
    {
        public int ReopenedFiscalYearId { get; set; }
        public string ReopenedFiscalYearTitle { get; set; } = default!;
        public int RemovedClosingVouchersCount { get; set; }
        public int RemovedOpeningVouchersCount { get; set; }
    }

    /// <summary>
    /// مدل تعریف یا ویرایش حساب بانکی / صندوق شرکت
    /// </summary>
    public class BankAccountCreateModel
    {
        public required string Title { get; set; }
        public required string BankName { get; set; }
        public string? AccountNumber { get; set; }
        public string? ShebaNumber { get; set; }
        public string? CardNumber { get; set; }
        public decimal InitialBalance { get; set; }
        public bool IsDefault { get; set; }
        public string? Description { get; set; }
    }

    public class BankAccountEditModel : BankAccountCreateModel
    {
        public int Id { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// گزارش خلاصه وضعیت و موجودی لحظه‌ای هر حساب بانکی / صندوق
    /// </summary>
    public class BankAccountBalanceSummaryDto
    {
        public int BankAccountId { get; set; }
        public string Title { get; set; } = default!;
        public string BankName { get; set; } = default!;
        public string? AccountNumber { get; set; }
        public string? ShebaNumber { get; set; }
        public string? CardNumber { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal CurrentBalance => TotalDeposits - TotalWithdrawals;
    }

    /// <summary>
    /// مدل ثبت اولیه چک دریافتنی (از مشتری) یا چک پرداختنی (صادره شرکت به تأمین‌کننده)
    /// </summary>
    public class ChequeCreateModel
    {
        public ChequeType Type { get; set; }
        public required string ChequeNumber { get; set; }
        public string? SayadiId { get; set; }
        public decimal Amount { get; set; }
        public required string IssueDateShamsi { get; set; }
        public required string DueDateShamsi { get; set; }
        public required string BankName { get; set; }
        public string? BranchName { get; set; }
        public string? DrawerName { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public int? ProjectId { get; set; }
        public int? BankAccountId { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// مدل تغییر وضعیت چک (واگذاری به بانک، وصول/پاس شدن، برگشت چک، خرج چک به تأمین‌کننده)
    /// </summary>
    public class ChequeStatusChangeModel
    {
        public int ChequeId { get; set; }
        public ChequeStatus NewStatus { get; set; }
        public string? ActionDateShamsi { get; set; }
        /// <summary>
        /// حساب بانکی شرکت جهت واگذاری چک دریافتنی به بانک یا وصول چک
        /// </summary>
        public int? TargetBankAccountId { get; set; }
        /// <summary>
        /// تأمین‌کننده مقصد در صورت خرج کردن چک دریافتنی (EndorsedToSupplier)
        /// </summary>
        public int? EndorsedToSupplierId { get; set; }
        public string? Description { get; set; }
    }

    /// <summary>
    /// ورودی محاسبه راس‌گیری چک‌ها (میانگین موزون زمانی و ریالی سررسید چک‌ها)
    /// </summary>
    public class ChequeRasGiriRequestModel
    {
        /// <summary>
        /// لیست شناسه چک‌های انتخابی (در صورت خالی بودن، بر اساس فیلتر نوع/مشتری/تأمین‌کننده محاسبه می‌شود)
        /// </summary>
        public List<int>? ChequeIds { get; set; }
        public ChequeType? Type { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        /// <summary>
        /// تاریخ مبنای راس‌گیری به شمسی (در صورت خالی بودن، تاریخ روز مبنا قرار می‌گیرد)
        /// </summary>
        public string? BaseDateShamsi { get; set; }
        /// <summary>
        /// نرخ سود / تنزیل ماهانه به درصد (مثلاً 2.5 درصد در ماه) جهت محاسبه ارزش فعلی و هزینه فرجه
        /// </summary>
        public decimal MonthlyDiscountRatePercent { get; set; } = 0m;
    }

    public class ChequeRasGiriItemDto
    {
        public int ChequeId { get; set; }
        public string ChequeNumber { get; set; } = default!;
        public string? SayadiId { get; set; }
        public string BankName { get; set; } = default!;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string DueDateShamsi { get; set; } = default!;
        /// <summary>
        /// فاصله روز سررسید چک تا تاریخ مبنا (مثبت = بعد از تاریخ مبنا، منفی = قبل از تاریخ مبنا)
        /// </summary>
        public int DaysFromBaseDate { get; set; }
        /// <summary>
        /// حاصل‌ضرب مبلغ چک در فاصله روز (Amount * DaysFromBaseDate)
        /// </summary>
        public decimal WeightedAmountDays { get; set; }
    }

    /// <summary>
    /// خروجی کامل موتور راس‌گیری چک‌ها (Weighted Average Maturity / Ras-Giri)
    /// </summary>
    public class ChequeRasGiriResultDto
    {
        public int ChequeCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime BaseDate { get; set; }
        public string BaseDateShamsi { get; set; } = default!;
        /// <summary>
        /// جمع کل (مبلغ × تعداد روز)
        /// </summary>
        public decimal TotalWeightedAmountDays { get; set; }
        /// <summary>
        /// راس چک‌ها به روز (میانگین موزون فاصله سررسیدها از تاریخ مبنا)
        /// </summary>
        public decimal WeightedAverageDays { get; set; }
        /// <summary>
        /// تاریخ دقیق راس چک‌ها (میلادی)
        /// </summary>
        public DateTime ExactRasDate { get; set; }
        /// <summary>
        /// تاریخ دقیق راس چک‌ها (شمسی)
        /// </summary>
        public string ExactRasDateShamsi { get; set; } = default!;
        public decimal MonthlyDiscountRatePercent { get; set; }
        /// <summary>
        /// مبلغ بهره / تنزیل دوره فرجه بر اساس تاریخ راس
        /// </summary>
        public decimal EstimatedDiscountAmount { get; set; }
        /// <summary>
        /// ارزش فعلی خالص چک‌ها در تاریخ مبنا پس از کسر تنزیل
        /// </summary>
        public decimal NetPresentValue => TotalAmount - EstimatedDiscountAmount;
        public List<ChequeRasGiriItemDto> Items { get; set; } = new();
    }

    // =========================================================================
    // مدل‌های فاز ۷: صورت‌حساب جامع طرف‌حساب‌ها (Customer/Supplier Statement) و تحلیل سنی مطالبات (Aging)
    // =========================================================================

    /// <summary>
    /// ردیف گردش در صورت‌حساب جامع مشتری یا تأمین‌کننده
    /// </summary>
    public class PartyStatementLineDto
    {
        public int VoucherId { get; set; }
        public int VoucherNumber { get; set; }
        public DateTime VoucherDate { get; set; }
        public string VoucherDateShamsi { get; set; } = default!;
        public string AccountCode { get; set; } = default!;
        public string AccountTitle { get; set; } = default!;
        public AccountingReferenceType ReferenceType { get; set; }
        public string ReferenceTypeTitle { get; set; } = default!;
        public int? ReferenceId { get; set; }
        public string? ProjectTitle { get; set; }
        public string Description { get; set; } = default!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public string BalanceNature => RunningBalance > 0 ? "بد" : RunningBalance < 0 ? "بس" : "تسویه";
    }

    /// <summary>
    /// گزارش جامع ۳۶۰ درجه صورت‌حساب مشتری یا تأمین‌کننده (شامل مانده اول دوره، ریز گردش، چک‌های در جریان و ریسک واقعی)
    /// </summary>
    public class PartyStatementReportDto
    {
        public AccountingPartyType PartyType { get; set; }
        public string PartyTypeTitle { get; set; } = default!;
        public int PartyId { get; set; }
        public string PartyName { get; set; } = default!;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// مانده انتقالی از قبل از تاریخ شروع گزارش (مثبت = بدهکار، منفی = بستانکار)
        /// </summary>
        public decimal OpeningBalance { get; set; }
        public string OpeningBalanceNature => OpeningBalance > 0 ? "بد" : OpeningBalance < 0 ? "بس" : "تسویه";

        /// <summary>
        /// جمع کل فاکتورهای فروش (برای مشتری) یا خرید (برای تأمین‌کننده) در دوره
        /// </summary>
        public decimal TotalInvoicesAmount { get; set; }

        /// <summary>
        /// جمع کل فاکتورهای برگشت از فروش یا برگشت از خرید در دوره
        /// </summary>
        public decimal TotalReturnsAmount { get; set; }

        /// <summary>
        /// جمع کل دریافت‌ها/پرداخت‌های نقدی و بانکی در دوره
        /// </summary>
        public decimal TotalCashAndBankSettlements { get; set; }

        /// <summary>
        /// تعداد و مبلغ چک‌های در جریان (نزد صندوق یا واگذار شده به بانک که هنوز پاس نشده‌اند)
        /// </summary>
        public int PendingChequesCount { get; set; }
        public decimal PendingChequesAmount { get; set; }

        /// <summary>
        /// مبلغ چک‌های برگشتی (واخواست‌شده) طرف‌حساب
        /// </summary>
        public decimal BouncedChequesAmount { get; set; }

        public decimal TotalPeriodDebit { get; set; }
        public decimal TotalPeriodCredit { get; set; }

        /// <summary>
        /// مانده نهایی دفتری (مثبت = بدهکار به شرکت، منفی = بستانکار از شرکت)
        /// </summary>
        public decimal FinalBookBalance { get; set; }
        public string FinalBalanceNature => FinalBookBalance > 0 ? "بدهکار" : FinalBookBalance < 0 ? "بستانکار" : "تسویه (بی‌حساب)";

        /// <summary>
        /// ریسک / تعهد واقعی باز (برای مشتری: مانده بدهکار دفتری + چک‌های دریافتنی هنوز وصول‌نشده)
        /// </summary>
        public decimal EffectiveExposureAmount { get; set; }

        public List<PartyStatementLineDto> Transactions { get; set; } = new();
    }

    /// <summary>
    /// ردیف تحلیل سنی مطالبات (مشتری) یا بدهی‌ها (تأمین‌کننده) به روش FIFO در ۵ بازه زمانی
    /// </summary>
    public class PartyAgingRowDto
    {
        public int PartyId { get; set; }
        public string PartyName { get; set; } = default!;
        public string? Phone { get; set; }

        /// <summary>
        /// مانده خالص بدهی باز (برای مشتری: مانده بدهکار، برای تأمین‌کننده: مانده بستانکار)
        /// </summary>
        public decimal TotalOpenBalance { get; set; }

        /// <summary>
        /// بازه ۱: جاری (۰ تا ۳۰ روز)
        /// </summary>
        public decimal Bucket0To30 { get; set; }

        /// <summary>
        /// بازه ۲: ۳۱ تا ۶۰ روز
        /// </summary>
        public decimal Bucket31To60 { get; set; }

        /// <summary>
        /// بازه ۳: ۶۱ تا ۹۰ روز
        /// </summary>
        public decimal Bucket61To90 { get; set; }

        /// <summary>
        /// بازه ۴: ۹۱ تا ۱۸۰ روز (معوق)
        /// </summary>
        public decimal Bucket91To180 { get; set; }

        /// <summary>
        /// بازه ۵: بیش از ۱۸۰ روز (راکد / مشکوک‌الوصول)
        /// </summary>
        public decimal BucketOver180 { get; set; }

        /// <summary>
        /// تاریخ قدیمی‌ترین فاکتور/بدهی تسویه‌نشده (به شمسی)
        /// </summary>
        public string? OldestOpenDateShamsi { get; set; }

        /// <summary>
        /// تعداد روز گذشته از قدیمی‌ترین بدهی باز
        /// </summary>
        public int MaxAgeDays { get; set; }

        /// <summary>
        /// میانگین موزون سنی بدهی به روز
        /// </summary>
        public decimal WeightedAverageAgeDays { get; set; }

        /// <summary>
        /// مبلغ چک‌های در جریان وصول این طرف‌حساب
        /// </summary>
        public decimal PendingChequesAmount { get; set; }

        /// <summary>
        /// وضعیت اعتباری طرف‌حساب بر اساس عمر بدهی
        /// </summary>
        public string CreditRiskStatus { get; set; } = default!;
    }

    /// <summary>
    /// خروجی کامل گزارش تحلیل سنی مطالبات مشتریان یا بدهی به تأمین‌کنندگان
    /// </summary>
    public class AgingReportSummaryDto
    {
        public AccountingPartyType PartyType { get; set; }
        public string PartyTypeTitle { get; set; } = default!;
        public DateTime AsOfDate { get; set; }
        public string AsOfDateShamsi { get; set; } = default!;
        public int ActivePartiesCount { get; set; }
        public decimal GrandTotalOpenBalance { get; set; }
        public decimal TotalBucket0To30 { get; set; }
        public decimal TotalBucket31To60 { get; set; }
        public decimal TotalBucket61To90 { get; set; }
        public decimal TotalBucket91To180 { get; set; }
        public decimal TotalBucketOver180 { get; set; }
        public List<PartyAgingRowDto> Rows { get; set; } = new();
    }
}