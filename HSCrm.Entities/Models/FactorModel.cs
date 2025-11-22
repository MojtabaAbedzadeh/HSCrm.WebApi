using HSCrm.Entities.Entities;

namespace HSCrm.Entities.Models
{
    public class FactorCreateModel
    {
        public int SupplierId { get; set; }
        public required string PurchasingUser { get; set; }
        public required string FactorNumber { get; set; }
        public byte FactorPayStatus { get; set; }
        public ApplicationUser User { get; set; } = default!;
        public int ProjectId { get; set; }
        public DateTime IssueDate { get; set; }
        public decimal Tax { get; set; }
        public decimal Discount { get; set; }
        public decimal Subtotal{ get; set; }
        public double total { get; set; }
        public int TenantId { get; set; }
        public required string CeratedById { get; set; }
        public DateTime CreatedAt{ get; set; }
        public List<InvoiceItemsCreateModel> purchaseInvoiceItem { get; set; } = new();
    }

    public class InvoiceItemsCreateModel
    {
        public int ProductId { get; set; }
        public int Count { get; set; }
        public decimal UnitPrice { get; set; } 
    }

    public class FactorEditModel
    {
        public int FactorId { get; set; }
        public string FactorNumber { get; set; } = default!;
        public DateTime FactorDate { get; set; }
        public byte FactorPayStatus { get; set; }
        public int SupplierId { get; set; }
        public string PurchasingUser { get; set; } = default!;
        public List<InvoiceItemsCreateModel> Items { get; set; } = new();
    }
}
