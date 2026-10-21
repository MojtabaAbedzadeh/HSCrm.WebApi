using HSCrm.Entities.Entities;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class ExpenseCreateModel
    {
        public required string ExpenseTitle { get; set; }
        public int ProjectId { get; set; }
        public ExpenseType Type { get; set; }
        public required string ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public int PaymentStatus { get; set; }
        public decimal PaidAmount { get; set; }
        public int? BankAccountId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNo { get; set; }

    }

    public class ExpenseEditModel
    {
        public int ExpenseId { get; set; }
        public required string ExpenseTitle { get; set; }
        public int ProjectId { get; set; }
        public ExpenseType Type { get; set; }
        public required string ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }

        public int PaymentStatus { get; set; }
        public decimal PaidAmount { get; set; }
        public int? BankAccountId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNo { get; set; }
    }
}
