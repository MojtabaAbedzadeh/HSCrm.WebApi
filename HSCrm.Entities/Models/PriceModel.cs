using HSCrm.Entities.Entities;
using System.ComponentModel.DataAnnotations;

namespace HSCrm.Entities.Models
{
    public class PriceModel
    {
        public class PriceCreateModel
        {
            public int ProductId { get; set; }
            public int TenantId { get; set; }
            public required string UserId { get; set; }
            public decimal SellPrice { get; set; }
            public bool IsActive { get; set; }
            public DateTime EffectiveFrom { get; set; }
            public DateTime? EffectiveTo { get; set; }
        }

        public class ProductPriceList
        {
            public int PriceId { get; set; }
            public decimal BuyPrice { get; set; }
            public decimal SellPrice { get; set; }
            public int ProductId { get; set; }
            public required string ProductName { get; set; }
            public DateTime EffectiveFrom { get; set; }
            public DateTime? EffectiveTo { get; set; }
            public required string UserId { get; set; }
            public ApplicationUser User { get; set; } = default!;
            public DateTime CreatedDate { get; set; }
            public bool IsActive { get; set; }
        }

        public class PriceEditModel
        {
            public int PriceId { get; set; }
            public required string UserId { get; set; }
            public int TenantId { get; set; }
            public long BuyPrice { get; set; }
            public decimal SellPrice { get; set; }
            public int ProductId { get; set; }
            public DateTime EffectiveFrom { get; set; }
            public DateTime? EffectiveTo { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
