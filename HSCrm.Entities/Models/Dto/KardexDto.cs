namespace HSCrm.Entities.Models.Dto
{
    public class KardexDto
    {
        public DateTime TransactionDate { get; set; }
        public string TransactionTypeTitle { get; set; } = string.Empty;
        public string ReferenceTypeTitle { get; set; } = string.Empty;
        public long ReferenceId { get; set; }
        public decimal QuantityChange { get; set; }
        public decimal InputQuantity { get; set; }
        public decimal OutputQuantity { get; set; }
        public decimal RunningBalance { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseTitle { get; set; } // به جای Id
        public string Description { get; set; } = string.Empty;
    }

    public class KardexReportFilterDto
    {
        public int ProductId { get; set; }
        public int? WarehouseId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}