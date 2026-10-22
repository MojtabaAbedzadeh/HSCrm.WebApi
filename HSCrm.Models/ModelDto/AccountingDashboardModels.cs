using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Models.ModelDto
{
    public class AccountingAccountDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public AccountLevel Level { get; set; }
        public AccountCategory Category { get; set; }
        public AccountNature Nature { get; set; }
        public bool IsSystemAccount { get; set; }
        public bool IsActive { get; set; }
        public string? Description { get; set; }
    }

    public class BankAccountDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? AccountNumber { get; set; }
        public string? CardNumber { get; set; }
        public string? ShebaNumber { get; set; }
        public decimal InitialBalance { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
    }

    public class BankAccountBalanceSummaryDto
    {
        public int BankAccountId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string? AccountNumber { get; set; }
        public string? CardNumber { get; set; }
        public string? ShebaNumber { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public decimal InitialBalance { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalWithdrawals { get; set; }
        public decimal CurrentBalance { get; set; }
    }

    public class ReceiptDto
    {
        public int Id { get; set; }
        public int SalesInvoiceId { get; set; }
        public string? SalesInvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }
        public decimal Amount { get; set; }
        public DateTime ReceiptDate { get; set; }
        public string? ReceiptDateShamsi { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? ReferenceNo { get; set; }
    }

    public class ChequeDto
    {
        public int Id { get; set; }
        public ChequeType Type { get; set; }
        public ChequeStatus Status { get; set; }
        public string ChequeNumber { get; set; } = string.Empty;
        public string? SayadiId { get; set; }
        public decimal Amount { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectTitle { get; set; }
        public int? BankAccountId { get; set; }
        public string? BankAccountTitle { get; set; }
        public string? Description { get; set; }
    }

    public class ReturnInvoiceSummaryDto
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime ReturnDate { get; set; }
        public byte Status { get; set; }
        public int WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectTitle { get; set; }
        public string? CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string? Description { get; set; }
    }
}
