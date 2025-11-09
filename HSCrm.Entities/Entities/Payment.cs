namespace HSCrm.Entities.Entities
{
    public class Payment : TenantScopedEntity
    {
        public int? SalesInvoiceId { get; set; }
        public SalesInvoice? SalesInvoice { get; set; }
        public int? PurchaseInvoiceId { get; set; }
        public PurchaseInvoice? PurchaseInvoice { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } = "Cash"; 
        public string? ReferenceNo { get; set; }
    }
}
