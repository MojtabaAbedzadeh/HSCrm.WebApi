using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class SupplierCreateModel
    {
        public required string SupplierFullName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }

    public class SupplierEditModel
    {
        public int SupplierId { get; set; }
        public required string SupplierFullName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
        public required string UserId { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public DateTime CreatedDate { get; set; }
    }
}
