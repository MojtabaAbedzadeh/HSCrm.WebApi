namespace HSCrm.Common.PublicTools
{
    public class Enums
    {
        public enum ExpenseType { Transport = 1, Food = 2, Accommodation = 3, Other = 4 }
        public enum InventoryTransactionType : byte
        {
            Purchase = 1,
            Sale = 2,
            Adjustment = 3,
            ReturnPurchase = 4,
            ReturnSale = 5,
            SaleRollback = 6,
            PurchaseRollback = 7
        }
        public enum InventoryReferenceType : byte
        {
            PurchaseInvoiceItem = 1,
            SalesInvoice= 2,
            SalesInvoiceItem = 3
        }
        public enum SalesInvoiceStatus : byte
        {
            Proforma = 0,   // پیش فاکتور
            Final = 1,      // فاکتور رسمی
            Cancelled = 2
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