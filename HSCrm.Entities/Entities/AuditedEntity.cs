using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Entities
{
    public abstract class AuditedEntity : EntityBase
    {
        public required DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public required string CreatedById { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public string? ModifiedById { get; set; }
        public bool IsDeleted { get; set; } = false;
        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }

    public abstract class EntityBase
    {
        [Key] public int Id { get; set; }
    }
}
