using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.Entities.Entities
{
    /// <summary>
    /// موجودیت چک‌های دریافتنی (از مشتریان) و پرداختنی (صادره شرکت به تأمین‌کنندگان/هزینه‌ها)
    /// همراه با پشتیبانی از چرخه واگذاری به بانک، وصول، برگشت، خرج چک و راس‌گیری
    /// </summary>
    public class Cheque : TenantScopedEntity, IFiscalYearScoped
    {
        public int FiscalYearId { get; set; }
        public FiscalYear FiscalYear { get; set; } = default!;

        /// <summary>
        /// نوع چک: دریافتنی (Receivable) یا پرداختنی (Payable)
        /// </summary>
        public ChequeType Type { get; set; }

        /// <summary>
        /// آخرین وضعیت چک در خزانه‌داری (نزد صندوق، واگذار شده به بانک، وصول شده، برگشتی، خرج شده)
        /// </summary>
        public ChequeStatus Status { get; set; } = ChequeStatus.InSafe;

        /// <summary>
        /// شماره سریال چک
        /// </summary>
        public required string ChequeNumber { get; set; }

        /// <summary>
        /// شناسه ۱۶ رقمی صیادی چک
        /// </summary>
        public string? SayadiId { get; set; }

        /// <summary>
        /// مبلغ چک (ریال)
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// تاریخ صدور / دریافت چک
        /// </summary>
        public DateTime IssueDate { get; set; }

        /// <summary>
        /// تاریخ سررسید چک (مبنای محاسبات راس‌گیری)
        /// </summary>
        public DateTime DueDate { get; set; }

        /// <summary>
        /// نام بانک صادرکننده چک (برای چک دریافتنی بانک مشتری، برای چک پرداختنی بانک شرکت)
        /// </summary>
        public required string BankName { get; set; }

        /// <summary>
        /// نام یا کد شعبه بانک
        /// </summary>
        public string? BranchName { get; set; }

        /// <summary>
        /// نام صاحب حساب / صادرکننده چک
        /// </summary>
        public string? DrawerName { get; set; }

        /// <summary>
        /// مشتری واگذارنده چک (برای چک دریافتنی)
        /// </summary>
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        /// <summary>
        /// تأمین‌کننده گیرنده چک (برای چک پرداختنی)
        /// </summary>
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        /// <summary>
        /// پروژه مرتبط با چک
        /// </summary>
        public int? ProjectId { get; set; }
        public Project? Project { get; set; }

        /// <summary>
        /// حساب بانکی شرکت:
        /// - در چک پرداختنی: حساب بانکی شرکت که دسته چک متعلق به آن است
        /// - در چک دریافتنی: حساب بانکی شرکت که چک جهت وصول (کلکسیون) به آن واگذار و واریز می‌شود
        /// </summary>
        public int? BankAccountId { get; set; }
        public BankAccount? BankAccount { get; set; }

        /// <summary>
        /// در صورتی که چک دریافتنی به یک تأمین‌کننده خرج (پشت‌نویسی) شود
        /// </summary>
        public int? EndorsedToSupplierId { get; set; }
        public Supplier? EndorsedToSupplier { get; set; }

        public DateTime? StatusChangedAt { get; set; }
        public string? Description { get; set; }
    }
}
