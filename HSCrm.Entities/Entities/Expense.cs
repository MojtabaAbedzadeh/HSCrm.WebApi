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
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;
        public ICollection<Payment> Payments { get; set; }
    }
}
