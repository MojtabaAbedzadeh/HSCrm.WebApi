using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Entities
{
    public class Supplier : TenantScopedEntity
    {
        public required string SupplierFullName { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierPhone { get; set; }
    }
}
