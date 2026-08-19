using HSCrm.Common.PublicTools;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Common.PublicTools
{
    public static class EnumExtensions
    {
        /// <summary>
        /// تبدیل نوع تراکنش انبار به نام فارسی
        /// </summary>
        public static string ToPersianName(this InventoryTransactionType transactionType)
        {
            return transactionType switch
            {
                InventoryTransactionType.Purchase => "خرید",
                InventoryTransactionType.Sale => "فروش",
                InventoryTransactionType.Adjustment => "تعدیل انبار",
                InventoryTransactionType.ReturnPurchase => "برگشت از خرید",
                InventoryTransactionType.ReturnSale => "برگشت از فروش",
                InventoryTransactionType.SaleRollback => "ابطال فروش",
                InventoryTransactionType.PurchaseRollback => "ابطال خرید",
                _ => "نامشخص"
            };
        }

        /// <summary>
        /// تبدیل نوع مرجع تراکنش به نام فارسی برای نمایش در کاردکس
        /// </summary>
        public static string ToPersianName(this InventoryReferenceType referenceType)
        {
            return referenceType switch
            {
                InventoryReferenceType.PurchaseInvoice => "فاکتور خرید",
                InventoryReferenceType.PurchaseInvoiceItem => "ردیف فاکتور خرید",
                InventoryReferenceType.SalesInvoice => "فاکتور فروش",
                InventoryReferenceType.SalesInvoiceItem => "ردیف فاکتور فروش",
                _ => "نامشخص"
            };
        }
    }
}
