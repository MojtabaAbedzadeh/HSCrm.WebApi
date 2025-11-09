namespace HSCrm.Entities.Entities
{
    public class Attachment : TenantScopedEntity
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public required string StoragePath { get; set; }
        public required string EntityName { get; set; } 
        public int EntityId { get; set; }
    }
}
