namespace HSCrm.Entities.Models.Dto
{
    public class ExpenseSummaryDto
    {
        public long ExpenseId { get; set; }
        public string ExpenseTitle { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public bool IsSettled { get; set; }
    }
}