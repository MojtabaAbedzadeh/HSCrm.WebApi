using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    public class Expense : TenantScopedEntity
    {
        public int ProjectId { get; set; }
        public Project Project { get; set; } = default!;
        public ExpenseType Type { get; set; }
        public DateTime ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }
}
