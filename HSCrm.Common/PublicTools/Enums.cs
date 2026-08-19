using System.ComponentModel;

namespace HSCrm.Common.PublicTools
{
    public class Enums
    {
        public enum ExpenseType { Transport = 1, Food = 2, Accommodation = 3, Other = 4 }
        public enum InventoryTransactionType : byte
        {
            [Description("خرید")] Purchase = 1,
            [Description("فروش")] Sale = 2,
            [Description("تعدیل موجودی")] Adjustment = 3,
            [Description("برگشت از خرید")] ReturnPurchase = 4,
            [Description("برگشت از فروش")] ReturnSale = 5,
            [Description("ابطال فروش")] SaleRollback = 6,
            [Description("ابطال خرید")] PurchaseRollback = 7
        }
        public enum InventoryReferenceType : byte
        {
            PurchaseInvoice = 0,
            PurchaseInvoiceItem = 1,
            SalesInvoice= 2,
            SalesInvoiceItem = 3,
            Adjustment = 4
        }
        public enum InvoiceStatus : byte
        {
            Proforma = 0,   // پیش فاکتور
            Final = 1,      // فاکتور رسمی
            Cancelled = 2,
            Rejected = 3
        }
        public enum SettlementStatus
        {
            Unpaid = 1,
            Partial = 2,
            Paid = 3
        }
        public enum PaymentMethod
        {
            Cash = 1,
            CardToCard = 2,
            POS = 3,
            BankTransfer = 4
        }
    }
}