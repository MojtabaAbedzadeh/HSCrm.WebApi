namespace HSCrm.Entities.Entities
{
    public class Product : TenantScopedEntity
    {
        public required string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();
    }
}
