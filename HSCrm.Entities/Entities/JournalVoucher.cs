using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// هدر سند حسابداری (Journal Voucher)
    /// </summary>
    public class JournalVoucher : TenantScopedEntity, IFiscalYearScoped
    {
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        /// <summary>
        /// شماره سند ترتیبی در سال مالی جاری
        /// </summary>
        public int VoucherNumber { get; set; }

        /// <summary>
        /// تاریخ سند حسابداری (میلادی در دیتابیس، شمسی در ورودی/خروجی)
        /// </summary>
        public DateTime VoucherDate { get; set; }

        /// <summary>
        /// شرح کلی سند حسابداری
        /// </summary>
        public required string Description { get; set; }

        public JournalVoucherStatus Status { get; set; } = JournalVoucherStatus.Draft;

        /// <summary>
        /// نوع مرجع صادرکننده سند (دستی، فاکتور فروش، فاکتور خرید، دریافت، پرداخت، هزینه)
        /// </summary>
        public AccountingReferenceType ReferenceType { get; set; } = AccountingReferenceType.Manual;

        /// <summary>
        /// شناسه رکورد مرجع (مثلاً SalesInvoice.Id یا Receipt.Id)
        /// </summary>
        public int? ReferenceId { get; set; }

        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }

        /// <summary>
        /// بررسی تراز بودن سند (جمع بدهکار برابر جمع بستانکار و بزرگتر از صفر)
        /// </summary>
        public bool IsBalanced => TotalDebit > 0 && TotalDebit == TotalCredit;

        public ICollection<JournalVoucherItem> Items { get; set; } = new List<JournalVoucherItem>();
    }

    /// <summary>
    /// آرتیکل (ردیف) سند حسابداری
    /// </summary>
    public class JournalVoucherItem : TenantScopedEntity
    {
        public int JournalVoucherId { get; set; }
        public JournalVoucher JournalVoucher { get; set; } = default!;

        public int RowNumber { get; set; }

        /// <summary>
        /// شناسه حساب معین
        /// </summary>
        public int AccountId { get; set; }
        public AccountingAccount Account { get; set; } = default!;

        /// <summary>
        /// شرح آرتیکل (ردیف سند)
        /// </summary>
        public required string Description { get; set; }

        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        // ============================================================
        // تفصیلی‌های شناور (متصل به موجودیت‌های فعلی HSCrm.WebApi)
        // ============================================================

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        public int? WarehouseId { get; set; }
        public Warehouse? Warehouse { get; set; }
    }
}