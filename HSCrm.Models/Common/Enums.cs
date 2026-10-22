using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
            SalesInvoice = 2,
            SalesInvoiceItem = 3,
            Adjustment = 4,
            SalesReturnInvoice = 5,
            PurchaseReturnInvoice = 6
        }

        public enum SalesInvoiceStatus : byte
        {
            Proforma = 0,
            Final = 1,
            Cancelled = 2
        }

        public enum InvoiceStatus : byte
        {
            Proforma = 0,
            Final = 1,
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
            BankTransfer = 4,
            Cheque = 5
        }

        public enum AccountLevel : byte
        {
            [Display(Name = "گروه حساب")] Group = 1,
            [Display(Name = "حساب کل")] General = 2,
            [Display(Name = "حساب معین")] Subsidiary = 3
        }

        public enum AccountCategory : byte
        {
            [Display(Name = "دارایی")] Asset = 1,
            [Display(Name = "بدهی")] Liability = 2,
            [Display(Name = "حقوق صاحبان سهام (سرمایه)")] Equity = 3,
            [Display(Name = "درآمد / فروش")] Revenue = 4,
            [Display(Name = "هزینه / بهای تمام‌شده")] Expense = 5
        }

        public enum AccountNature : byte
        {
            [Display(Name = "بدهکار")] DebitOnly = 1,
            [Display(Name = "بستانکار")] CreditOnly = 2,
            [Display(Name = "شناور (بدهکار/بستانکار)")] Both = 3
        }

        public enum JournalVoucherStatus : byte
        {
            [Display(Name = "پیش‌نویس")] Draft = 0,
            [Display(Name = "ثبت قطعی")] Posted = 1,
            [Display(Name = "ابطال شده")] Cancelled = 2
        }

        public enum AccountingReferenceType : byte
        {
            [Display(Name = "سند دستی")] Manual = 0,
            [Display(Name = "فاکتور فروش")] SalesInvoice = 1,
            [Display(Name = "فاکتور خرید")] PurchaseInvoice = 2,
            [Display(Name = "دریافت خزانه")] Receipt = 3,
            [Display(Name = "پرداخت خزانه")] Payment = 4,
            [Display(Name = "ثبت هزینه پروژه")] Expense = 5,
            [Display(Name = "سند افتتاحیه")] OpeningBalance = 6,
            [Display(Name = "عملیات چک و اسناد تجاری")] ChequeOperation = 7,
            [Display(Name = "سند اختتامیه")] ClosingBalance = 8,
            [Display(Name = "فاکتور برگشت از فروش")] SalesReturnInvoice = 9,
            [Display(Name = "فاکتور برگشت از خرید")] PurchaseReturnInvoice = 10
        }

        public enum InventoryCostingMethod : byte
        {
            [Display(Name = "میانگین موزون متحرک")] WeightedAverage = 1,
            [Display(Name = "اولین صادره از اولین وارده (FIFO)")] FIFO = 2
        }

        public enum ChequeType : byte
        {
            [Display(Name = "چک دریافتنی (از مشتری)")] Receivable = 1,
            [Display(Name = "چک پرداختنی (شرکت)")] Payable = 2
        }

        public enum ChequeStatus : byte
        {
            [Display(Name = "نزد صندوق / در جریان")] InSafe = 1,
            [Display(Name = "واگذار شده به بانک (در جریان وصول)")] InBankForCollection = 2,
            [Display(Name = "وصول شده / پاس شده")] Cleared = 3,
            [Display(Name = "برگشت خورده (واخواست)")] Bounced = 4,
            [Display(Name = "خرج شده به تأمین‌کننده")] EndorsedToSupplier = 5,
            [Display(Name = "عودت داده شده / باطل شده")] ReturnedOrCancelled = 6
        }

        public enum AccountingPartyType : byte
        {
            [Display(Name = "مشتری (حساب دریافتنی)")] Customer = 1,
            [Display(Name = "تأمین‌کننده (حساب پرداختنی)")] Supplier = 2
        }
    }
}
