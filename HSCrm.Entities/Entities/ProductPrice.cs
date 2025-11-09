namespace HSCrm.Entities.Entities
{
    public class ProductPrice : TenantScopedEntity
    {
        public int ProductId { get; set; }
        public Product Product { get; set; } = default!;
        public required decimal SellPrice { get; set; }
        public bool IsActive { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime? EffectiveTo { get; set; }
    }
}
