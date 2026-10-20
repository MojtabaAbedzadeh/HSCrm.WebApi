using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// جدول کدینگ حساب‌ها (درختواره گروه، کل، معین)
    /// </summary>
    public class AccountingAccount : TenantScopedEntity
    {
        /// <summary>
        /// کد حساب (مثال: 1101 برای صندوق و بانک، 1103 برای حساب‌های دریافتنی)
        /// </summary>
        public required string Code { get; set; }

        /// <summary>
        /// عنوان فارسی حساب
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// سطح حساب: گروه (1)، کل (2)، معین (3)
        /// فقط حساب‌های سطح معین (Subsidiary) در آرتیکل سند قابل انتخاب هستند
        /// </summary>
        public AccountLevel Level { get; set; }

        /// <summary>
        /// طبقه اصلی حساب (دارایی، بدهی، سرمایه، درآمد، هزینه)
        /// </summary>
        public AccountCategory Category { get; set; }

        /// <summary>
        /// ماهیت حساب (بدهکار، بستانکار، شناور)
        /// </summary>
        public AccountNature Nature { get; set; }

        /// <summary>
        /// ارتباط درختی با حساب والد
        /// </summary>
        public int? ParentId { get; set; }
        public AccountingAccount? Parent { get; set; }

        /// <summary>
        /// زیرمجموعه‌های این حساب
        /// </summary>
        public ICollection<AccountingAccount> Children { get; set; } = new List<AccountingAccount>();

        /// <summary>
        /// آیا حساب سیستمی و پیش‌فرض است؟ (حساب‌های سیستمی قابل حذف نیستند)
        /// </summary>
        public bool IsSystemAccount { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Description { get; set; }

        public ICollection<JournalVoucherItem> VoucherItems { get; set; } = new List<JournalVoucherItem>();
    }
}