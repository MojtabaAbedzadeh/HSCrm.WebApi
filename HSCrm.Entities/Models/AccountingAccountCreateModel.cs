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
        public int? ProfitLossClosingVoucherNumber { get; set; }
        public int? ClosingVoucherNumber { get; set; }
        public int? OpeningVoucherNumberInNewYear { get; set; }
        public decimal TotalPermanentBalanceTransferred { get; set; }
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
}
