using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class FactorCreateModel2
    {
        public int SupplierId { get; set; }
        public required string PurchasingUser { get; set; }
        public decimal FactorSumPrice { get; set; }   // decimal
        public required string FactorNumber { get; set; }
        public byte FactorPayStatus { get; set; }
        public DateTime FactorDate { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public required string UserId { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class FactorItemCreateModel
    {
        public int ProductId { get; set; }
        public int Count { get; set; }
        public decimal UnitPrice { get; set; }  // یا TotalPrice اگر ردیفی است
    }

    public class FactorCreateModel
    {
        public string FactorNumber { get; set; } = default!;
        public DateTime FactorDate { get; set; }
        public byte FactorPayStatus { get; set; }
        public int SupplierId { get; set; }
        public string PurchasingUser { get; set; } = default!;
        public List<FactorItemCreateModel> Items { get; set; } = new();
    }


    public class FactorEditModel
    {
        public int FactorId { get; set; }
        public string FactorNumber { get; set; } = default!;
        public DateTime FactorDate { get; set; }
        public byte FactorPayStatus { get; set; }
        public int SupplierId { get; set; }
        public string PurchasingUser { get; set; } = default!;
        public List<FactorItemCreateModel> Items { get; set; } = new();
    }
}
