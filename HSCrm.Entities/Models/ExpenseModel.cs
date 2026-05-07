using HSCrm.Entities.Entities;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models
{
    public class ExpenseCreateModel
    {
        public required string ExpenseTitle { get; set; }
        public int ProjectId { get; set; }
        public int TenantId { get; set; }
        public ExpenseType Type { get; set; }
        public required string ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public required string UserId { get; set; }
    }

    public class ExpenseEditModel
    {
        public int ExpenseId { get; set; }
        public required string ExpenseTitle { get; set; }
        public int ProjectId { get; set; }
        public int TenantId { get; set; }
        public ExpenseType Type { get; set; }
        public required string ExpenseDate { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public required string ModifiedById { get; set; }
    }
}
