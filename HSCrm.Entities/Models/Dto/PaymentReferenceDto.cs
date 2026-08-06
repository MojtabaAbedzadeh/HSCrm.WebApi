namespace HSCrm.Entities.Models.Dto
{
    public class PaymentReferenceDto
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // purchaseInvoice | expense
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
    }
}