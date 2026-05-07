namespace HSCrm.Entities.Entities
{
    public class FiscalYear : TenantScopedEntity
    {
        public required string Title { get; set; }      // مثلا: 1404
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsClosed { get; set; }
        public bool IsCurrent { get; set; }
    }
}
