using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Models.Dto
{
    public  class ExpenseListDto
    {
        public int Id { get; set; }
        public string ExpenseTitle { get; set; } = default!;
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public ExpenseType Type { get; set; }
        public string ProjectTitle { get; set; } = default!;
        public string CreatedByFullName { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
