using System.ComponentModel.DataAnnotations.Schema;

namespace HSCrm.Entities.Entities
{
    [Table("Warehouse")]
    public class Warehouse : TenantScopedEntity
    {
        public required string Title { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
