using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class ProductCreateModel
    {
        public required string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public required string UserId { get; set; }
        public required int TenantId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }

    public class ProductEditModel
    {
        public int ProductId { get; set; }
        public required string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }
}
