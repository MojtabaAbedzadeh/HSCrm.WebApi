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
            BankTransfer = 4,
            Cheque = 5
        }

        /// <summary>
        /// سطح حساب در ساختار درختی کدینگ حسابداری
        /// </summary>
        public enum AccountLevel : byte
        {
            [Display(Name = "گروه حساب")]
            Group = 1,

            [Display(Name = "حساب کل")]
            General = 2,

            [Display(Name = "حساب معین")]
            Subsidiary = 3
        }

        /// <summary>
        /// نوع اصلی حساب در گزارشات مالی (ترازنامه و سود و زیان)
        /// </summary>
        public enum AccountCategory : byte
        {
            [Display(Name = "دارایی")]
            Asset = 1,

            [Display(Name = "بدهی")]
            Liability = 2,

            [Display(Name = "حقوق صاحبان سهام / سرمایه")]
            Equity = 3,

            [Display(Name = "درآمد / فروش")]
            Revenue = 4,

            [Display(Name = "بهای تمام‌شده و هزینه")]
            Expense = 5
        }

        /// <summary>
        /// ماهیت کنترلی حساب (جلوگیری از مانده خلاف ماهیت)
        /// </summary>
        public enum AccountNature : byte
        {
            [Display(Name = "بدهکار")]
            DebitOnly = 1,

            [Display(Name = "بستانکار")]
            CreditOnly = 2,

            [Display(Name = "شناور (بدهکار/بستانکار)")]
            Both = 3
        }

        /// <summary>
        /// وضعیت سند حسابداری (Journal Voucher)
        /// </summary>
        public enum JournalVoucherStatus : byte
        {
            [Display(Name = "پیش‌نویس (موقت)")]
            Draft = 1,

            [Display(Name = "ثبت قطعی (دائم)")]
            Posted = 2,

            [Display(Name = "باطل شده")]
            Cancelled = 3
        }

        /// <summary>
        /// روش قیمت‌گذاری موجودی انبار و محاسبه بهای تمام‌شده کالای فروش‌رفته (COGS)
        /// </summary>
        public enum InventoryCostingMethod : byte
        {
            [Display(Name = "میانگین موزون متحرک (Moving Weighted Average)")]
            WeightedAverage = 1,

            [Display(Name = "اولین صادره از اولین وارده (FIFO)")]
            FIFO = 2
        }

        /// <summary>
        /// نوع چک تجاری (دریافتنی از مشتری یا پرداختنی به تأمین‌کننده/هزینه)
        /// </summary>
        public enum ChequeType : byte
        {
            [Display(Name = "چک دریافتنی (اسناد دریافتنی)")]
            Receivable = 1,

            [Display(Name = "چک پرداختنی (اسناد پرداختنی)")]
            Payable = 2
        }

        /// <summary>
        /// وضعیت چرخه حیات چک در خزانه‌داری
        /// </summary>
        public enum ChequeStatus : byte
        {
            [Display(Name = "نزد صندوق / در جریان")]
            InSafe = 1,

            [Display(Name = "واگذار شده به بانک (در جریان وصول)")]
            InBankForCollection = 2,

            [Display(Name = "وصول شده / پاس شده")]
            Cleared = 3,

            [Display(Name = "برگشت خورده (واخواست)")]
            Bounced = 4,

            [Display(Name = "خرج شده به تأمین‌کننده")]
            EndorsedToSupplier = 5,

            [Display(Name = "عودت داده شده / باطل شده")]
            Returned = 6
        }

        /// <summary>
        /// منشأ صدور سند حسابداری (دستی یا خودکار از زیرسیستم‌های HSCrm)
        /// </summary>
        public enum AccountingReferenceType : byte
        {
            [Display(Name = "سند دستی")]
            Manual = 1,

            [Display(Name = "فاکتور فروش")]
            SalesInvoice = 2,

            [Display(Name = "فاکتور خرید")]
            PurchaseInvoice = 3,

            [Display(Name = "دریافت خزانه")]
            Receipt = 4,

            [Display(Name = "پرداخت خزانه")]
            Payment = 5,

            [Display(Name = "ثبت هزینه پروژه")]
            Expense = 6,

            [Display(Name = "سند افتتاحیه")]
            OpeningBalance = 7,

            [Display(Name = "سند اختتامیه")]
            ClosingBalance = 8,

            [Display(Name = "عملیات چک و اسناد تجاری")]
            ChequeOperation = 9,

            [Display(Name = "فاکتور برگشت از فروش")]
            SalesReturnInvoice = 10,

            [Display(Name = "فاکتور برگشت از خرید")]
            PurchaseReturnInvoice = 11
        }
    }
}
