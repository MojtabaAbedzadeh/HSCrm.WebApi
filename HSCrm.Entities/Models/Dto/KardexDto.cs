using static HSCrm.Common.PublicTools.Enums;

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

        /// <summary>
        /// فی بهای تمام‌شده ورود یا خروج (ریال)
        /// </summary>
        public decimal UnitCost { get; set; }

        /// <summary>
        /// ارزش ریالی کل ورود یا خروج (ریال)
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// نرخ میانگین موزون هر واحد پس از این تراکنش (ریال)
        /// </summary>
        public decimal RunningUnitCost { get; set; }

        /// <summary>
        /// مانده ریالی انبار پس از این تراکنش (ریال)
        /// </summary>
        public decimal RunningValueBalance { get; set; }

        public int WarehouseId { get; set; }
        public string WarehouseTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class KardexReportFilterDto
    {
        public int ProductId { get; set; }
        public int? WarehouseId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    /// <summary>
    /// گزارش ارزش‌گذاری موجودی انبار و مقایسه بهای تمام‌شده (میانگین موزون و FIFO)
    /// </summary>
    public class InventoryValuationDto
    {
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public int WarehouseId { get; set; }
        public string WarehouseTitle { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public decimal WeightedAverageUnitCost { get; set; }
        public decimal WeightedAverageTotalValue => Math.Round(CurrentQuantity * WeightedAverageUnitCost, 2);
        public decimal FifoTotalValue { get; set; }
        public decimal FifoEffectiveUnitCost => CurrentQuantity > 0 ? Math.Round(FifoTotalValue / CurrentQuantity, 2) : 0m;
    }
}
