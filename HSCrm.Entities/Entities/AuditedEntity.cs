using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Entities
{
    public abstract class AuditedEntity : EntityBase
    {
        public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedById { get; set; }
        public ApplicationUser CreatedBy { get; set; } = default!;
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedById { get; set; }
        public ApplicationUser? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedById { get; set; }
        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public abstract class EntityBase
    {
        [Key] public int Id { get; set; }
    }
}