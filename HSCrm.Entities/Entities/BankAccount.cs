namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// موجودیت حساب‌های بانکی و صندوق‌های شرکت (خزانه‌داری)
    /// </summary>
    public class BankAccount : TenantScopedEntity
    {
        /// <summary>
        /// عنوان نمایشی حساب یا صندوق (مثال: بانک ملت - جاری شرکت، یا صندوق تنخواه پروژه)
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// نام بانک (مثال: بانک ملت، بانک سامان، صندوق نقدی)
        /// </summary>
        public required string BankName { get; set; }

        /// <summary>
        /// شماره حساب بانکی
        /// </summary>
        public string? AccountNumber { get; set; }

        /// <summary>
        /// شماره شبا (۲۴ رقم یا همراه با IR)
        /// </summary>
        public string? ShebaNumber { get; set; }

        /// <summary>
        /// شماره کارت ۱۶ رقمی
        /// </summary>
        public string? CardNumber { get; set; }

        /// <summary>
        /// موجودی اولیه حساب در ابتدای تعریف
        /// </summary>
        public decimal InitialBalance { get; set; }

        /// <summary>
        /// آیا حساب پیش‌فرض دریافت و پرداخت‌های شرکت است؟
        /// </summary>
        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Description { get; set; }
    }
}
