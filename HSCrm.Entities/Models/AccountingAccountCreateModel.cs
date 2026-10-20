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
    }

    /// <summary>
    /// خروجی تراز آزمایشی (Trial Balance)
    /// </summary>
    public class TrialBalanceRowDto
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = default!;
        public string AccountTitle { get; set; } = default!;
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
}