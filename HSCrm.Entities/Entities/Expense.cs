using System.ComponentModel.DataAnnotations.Schema;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class Expense : TenantScopedEntity, IFiscalYearScoped
    {
        public required string ExpenseTitle { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;
        public ExpenseType Type { get; set; }
        public DateTime ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        /// <summary>
        /// 0 -> بدون پرداخت
        /// 1 -> پرداخت بخشی
        /// 2 -> پرداخت کامل
        /// </summary>
        public int PaymentStatus { get; set; }
        public decimal PaidAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNo { get; set; }

        public int? BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        [NotMapped]
        public string? CreatedByFullName => CreatedBy != null ? $"{CreatedBy.UserFirstName} {CreatedBy.UserLastName}".Trim() : null;
    }
}