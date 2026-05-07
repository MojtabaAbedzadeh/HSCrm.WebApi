namespace HSCrm.Entities.Models
{
    public class ProductCreateModel
    {
        public required string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public required string UserId { get; set; }
        public bool IsActive { get; set; }
        public required int TenantId { get; set; }
    }

    public class ProductEditModel
    {
        public int ProductId { get; set; }
        public required string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public bool IsActive { get; set; }
        public required string ModifiedById { get; set; }
    }

    public class ProductWithPriceDto
    {
        public int ProductId { get; set; }
        public string ProductTitle { get; set; }
        public required string ProductUnit { get; set; }
        public bool IsActive { get; set; }
        public decimal? SellPrice { get; set; }
        public decimal? BuyPrice { get; set; }
        public required int TenantId { get; set; }
    }

}
