using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using SinaPharm.Common;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class AccountingService : IAccountingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ICurrentFiscalYearService _currentFiscalYear;

        // کدهای استاندارد حساب‌های معین سیستمی
        public const string ACC_CASH_BANK = "1101";          // موجودی نقد و بانک
        public const string ACC_NOTES_RECEIVABLE = "1102";   // اسناد دریافتنی نزد صندوق (چک‌های دریافتنی از مشتریان)
        public const string ACC_NOTES_IN_COLLECTION = "1107";// اسناد دریافتنی در جریان وصول (واگذار شده به بانک)
        public const string ACC_RECEIVABLE = "1103";         // حساب‌های دریافتنی تجاری (مشتریان)
        public const string ACC_INVENTORY = "1105";          // موجودی مواد و کالا (انبار)
        public const string ACC_VAT_RECEIVABLE = "1106";     // مالیات بر ارزش افزوده خرید
        public const string ACC_PAYABLE = "2101";            // حساب‌های پرداختنی تجاری (تأمین‌کنندگان)
        public const string ACC_VAT_PAYABLE = "2102";        // مالیات بر ارزش افزوده فروش
        public const string ACC_NOTES_PAYABLE = "2103";      // اسناد پرداختنی تجاری (چک‌های صادره شرکت)
        public const string ACC_EQUITY = "3101";             // سرمایه اولیه
        public const string ACC_SALES_REVENUE = "4101";      // درآمد فروش کالا و خدمات
        public const string ACC_SALES_DISCOUNT = "4102";     // تخفیفات فروش
        public const string ACC_PURCHASE_COST = "5101";      // خرید کالا / بهای تمام‌شده
        public const string ACC_PURCHASE_DISCOUNT = "5102";  // تخفیفات خرید
        public const string ACC_PROJECT_EXPENSE = "5201";    // هزینه‌های اجرایی و عملیاتی پروژه

        public AccountingService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ICurrentFiscalYearService currentFiscalYear)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _currentFiscalYear = currentFiscalYear;
        }

        public async Task EnsureDefaultChartOfAccountsAsync(int tenantId, CancellationToken ct = default)
        {
            var exists = await _context.AccountingAccounts
                .IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);

            if (exists) return;

            var defaultAccounts = new List<AccountingAccount>
            {
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_CASH_BANK, Title = "موجودی نقد و بانک (خزانه)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_NOTES_RECEIVABLE, Title = "اسناد دریافتنی نزد صندوق (چک‌های دریافتنی)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_NOTES_IN_COLLECTION, Title = "اسناد دریافتنی در جریان وصول (واگذار شده به بانک)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_RECEIVABLE, Title = "حساب‌های دریافتنی تجاری (مشتریان و پروژه‌ها)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.Both, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_INVENTORY, Title = "موجودی کالا و انبار", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_VAT_RECEIVABLE, Title = "پیش‌پرداخت مالیات ارزش افزوده (خرید)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_PAYABLE, Title = "حساب‌های پرداختنی تجاری (تأمین‌کنندگان و هزینه‌ها)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Liability, Nature = AccountNature.Both, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_VAT_PAYABLE, Title = "مالیات بر ارزش افزوده فروش (پرداختنی)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Liability, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_NOTES_PAYABLE, Title = "اسناد پرداختنی تجاری (چک‌های پرداختنی شرکت)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Liability, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_EQUITY, Title = "سرمایه و سود انباشته", Level = AccountLevel.Subsidiary, Category = AccountCategory.Equity, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_SALES_REVENUE, Title = "درآمد فروش کالا و خدمات پروژه", Level = AccountLevel.Subsidiary, Category = AccountCategory.Revenue, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_SALES_DISCOUNT, Title = "تخفیفات فروش", Level = AccountLevel.Subsidiary, Category = AccountCategory.Revenue, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_PURCHASE_COST, Title = "خرید کالا و بهای تمام‌شده", Level = AccountLevel.Subsidiary, Category = AccountCategory.Expense, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_PURCHASE_DISCOUNT, Title = "تخفیفات خرید", Level = AccountLevel.Subsidiary, Category = AccountCategory.Expense, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_PROJECT_EXPENSE, Title = "هزینه‌های اجرایی، پرسنلی و عمومی پروژه", Level = AccountLevel.Subsidiary, Category = AccountCategory.Expense, Nature = AccountNature.DebitOnly, IsSystemAccount = true }
            };

            await _context.AccountingAccounts.AddRangeAsync(defaultAccounts, ct);
            await _context.SaveChangesAsync(ct);
        }

        private async Task<int> GetNextVoucherNumberAsync(int tenantId, int fiscalYearId, CancellationToken ct)
        {
            var lastNumber = await _context.JournalVouchers
                .Where(x => x.TenantId == tenantId && x.FiscalYearId == fiscalYearId && !x.IsDeleted)
                .MaxAsync(x => (int?)x.VoucherNumber, ct) ?? 0;

            return lastNumber + 1;
        }

        private async Task<AccountingAccount> GetSystemAccountAsync(int tenantId, string code, CancellationToken ct)
        {
            await EnsureDefaultChartOfAccountsAsync(tenantId, ct);

            var account = await _context.AccountingAccounts
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, ct);

            if (account == null)
            {
                if (code == ACC_NOTES_RECEIVABLE || code == ACC_NOTES_IN_COLLECTION || code == ACC_NOTES_PAYABLE)
                {
                    account = new AccountingAccount
                    {
                        CreatedAt = DateTime.UtcNow,
                        TenantId = tenantId,
                        Code = code,
                        Title = code == ACC_NOTES_RECEIVABLE
                            ? "اسناد دریافتنی نزد صندوق (چک‌های دریافتنی)"
                            : code == ACC_NOTES_IN_COLLECTION
                                ? "اسناد دریافتنی در جریان وصول (واگذار شده به بانک)"
                                : "اسناد پرداختنی تجاری (چک‌های پرداختنی شرکت)",
                        Level = AccountLevel.Subsidiary,
                        Category = code == ACC_NOTES_PAYABLE ? AccountCategory.Liability : AccountCategory.Asset,
                        Nature = code == ACC_NOTES_PAYABLE ? AccountNature.CreditOnly : AccountNature.DebitOnly,
                        IsSystemAccount = true,
                        IsActive = true
                    };
                    await _context.AccountingAccounts.AddAsync(account, ct);
                    await _context.SaveChangesAsync(ct);
                    return account;
                }

                throw new InvalidOperationException($"حساب سیستمی با کد {code} یافت نشد.");
            }

            return account;
        }

        private async Task<BankAccount?> ResolveBankAccountAsync(int tenantId, int? requestedBankAccountId, CancellationToken ct)
        {
            if (requestedBankAccountId.HasValue && requestedBankAccountId.Value > 0)
            {
                var specific = await _context.Set<BankAccount>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == requestedBankAccountId.Value && x.TenantId == tenantId && !x.IsDeleted, ct);
                if (specific != null) return specific;
            }

            var defaultBank = await _context.Set<BankAccount>()
                .AsNoTracking()
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.IsActive && !x.IsDeleted, ct);

            return defaultBank;
        }

        public async Task<JournalVoucher> CreateManualVoucherAsync(JournalVoucherCreateModel model, CancellationToken ct = default)
        {
            var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("شناسه شرکت یافت نشد.");
            var fiscalYearId = _currentFiscalYear.FiscalYearId ?? throw new InvalidOperationException("سال مالی فعال یافت نشد.");

            if (model.Items == null || model.Items.Count < 2)
                throw new InvalidOperationException("سند حسابداری باید حداقل دارای ۲ آرتیکل باشد.");

            var totalDebit = model.Items.Sum(x => x.Debit);
            var totalCredit = model.Items.Sum(x => x.Credit);

            if (totalDebit <= 0 || totalDebit != totalCredit)
                throw new InvalidOperationException("سند حسابداری تراز نیست. جمع بدهکار باید با جمع بستانکار برابر و بزرگتر از صفر باشد.");

            var voucherDate = ConvertDate.ConvertShamsiToMiladi(model.VoucherDateShamsi);
            var nextNumber = await GetNextVoucherNumberAsync(tenantId, fiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                VoucherNumber = nextNumber,
                VoucherDate = voucherDate,
                Description = model.Description.Trim(),
                Status = model.PostImmediately ? JournalVoucherStatus.Posted : JournalVoucherStatus.Draft,
                ReferenceType = AccountingReferenceType.Manual,
                TotalDebit = totalDebit,
                CreatedAt = DateTime.UtcNow,
                TotalCredit = totalCredit,
                Items = model.Items.Select((item, idx) => new JournalVoucherItem
                {
                    TenantId = tenantId,
                    RowNumber = idx + 1,
                    CreatedAt = DateTime.UtcNow,
                    AccountId = item.AccountId,
                    Description = item.Description.Trim(),
                    Debit = item.Debit,
                    Credit = item.Credit,
                    CustomerId = item.CustomerId,
                    SupplierId = item.SupplierId,
                    ProjectId = item.ProjectId,
                    WarehouseId = item.WarehouseId,
                    BankAccountId = item.BankAccountId
                }).ToList()
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForSalesInvoiceAsync(
            SalesInvoice invoice,
            InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage,
            CancellationToken ct = default)
        {
            var accReceivable = await GetSystemAccountAsync(invoice.TenantId, ACC_RECEIVABLE, ct);
            var accSales = await GetSystemAccountAsync(invoice.TenantId, ACC_SALES_REVENUE, ct);
            var accDiscount = await GetSystemAccountAsync(invoice.TenantId, ACC_SALES_DISCOUNT, ct);
            var accVat = await GetSystemAccountAsync(invoice.TenantId, ACC_VAT_PAYABLE, ct);
            var accCogs = await GetSystemAccountAsync(invoice.TenantId, ACC_PURCHASE_COST, ct);    // 5101: بهای تمام‌شده کالای فروش‌رفته (COGS)
            var accInventory = await GetSystemAccountAsync(invoice.TenantId, ACC_INVENTORY, ct);   // 1105: موجودی مواد و کالا (انبار)

            var project = await _context.Set<Project>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == invoice.ProjectId, ct);

            int? customerId = project?.CustomerId;

            var items = new List<JournalVoucherItem>();
            int row = 1;

            items.Add(new JournalVoucherItem
            {
                TenantId = invoice.TenantId,
                RowNumber = row++,
                CreatedAt = DateTime.UtcNow,
                AccountId = accReceivable.Id,
                Description = $"بابت فاکتور فروش شماره {invoice.Number}",
                Debit = invoice.Total,
                Credit = 0,
                CustomerId = customerId,
                ProjectId = invoice.ProjectId
            });

            if (invoice.Discount > 0)
            {
                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    AccountId = accDiscount.Id,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"تخفیف فاکتور فروش شماره {invoice.Number}",
                    Debit = invoice.Discount,
                    Credit = 0,
                    ProjectId = invoice.ProjectId
                });
            }

            items.Add(new JournalVoucherItem
            {
                TenantId = invoice.TenantId,
                RowNumber = row++,
                AccountId = accSales.Id,
                Description = $"درآمد حاصل از فاکتور فروش شماره {invoice.Number}",
                Debit = 0,
                Credit = invoice.Subtotal,
                CreatedAt = DateTime.UtcNow,
                ProjectId = invoice.ProjectId,
                WarehouseId = invoice.WarehouseId
            });

            if (invoice.Tax > 0)
            {
                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    AccountId = accVat.Id,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"مالیات بر ارزش افزوده فاکتور فروش شماره {invoice.Number}",
                    Debit = 0,
                    Credit = invoice.Tax,
                    ProjectId = invoice.ProjectId
                });
            }

            // =========================================================================
            // محاسبه و ثبت خودکار بهای تمام‌شده کالای فروش‌رفته (COGS - میانگین موزون یا FIFO)
            // =========================================================================
            decimal totalCogs = 0m;
            if (invoice.Items != null && invoice.Items.Any())
            {
                foreach (var saleItem in invoice.Items)
                {
                    if (saleItem.TotalCost > 0)
                    {
                        totalCogs += saleItem.TotalCost;
                    }
                    else
                    {
                        // در صورتی که قبلاً توسط InventoryService محاسبه نشده باشد، از نرخ میانگین موزون انبار استخراج می‌شود
                        var inv = await _context.Inventories
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.TenantId == invoice.TenantId
                                                   && x.WarehouseId == invoice.WarehouseId
                                                   && x.ProductId == saleItem.ProductId, ct);

                        decimal unitCost = inv?.AverageUnitCost ?? 0m;
                        decimal lineCost = Math.Round(saleItem.Quantity * unitCost, 2);
                        saleItem.UnitCost = unitCost;
                        saleItem.TotalCost = lineCost;
                        totalCogs += lineCost;
                    }
                }
            }

            if (totalCogs > 0)
            {
                var methodTitle = costingMethod == InventoryCostingMethod.FIFO ? "FIFO" : "میانگین موزون";
                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    AccountId = accCogs.Id,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"بهای تمام‌شده کالای فروش‌رفته ({methodTitle}) بابت فاکتور {invoice.Number}",
                    Debit = totalCogs,
                    Credit = 0,
                    ProjectId = invoice.ProjectId,
                    WarehouseId = invoice.WarehouseId
                });

                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    AccountId = accInventory.Id,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"کسر ارزش موجودی انبار ({methodTitle}) بابت فاکتور فروش {invoice.Number}",
                    Debit = 0,
                    Credit = totalCogs,
                    ProjectId = invoice.ProjectId,
                    WarehouseId = invoice.WarehouseId
                });
            }

            var nextNumber = await GetNextVoucherNumberAsync(invoice.TenantId, invoice.FiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = invoice.TenantId,
                FiscalYearId = invoice.FiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = invoice.IssueDate,
                Description = $"سند خودکار فاکتور فروش و بهای تمام‌شده (COGS) شماره {invoice.Number}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.SalesInvoice,
                ReferenceId = invoice.Id,
                TotalDebit = items.Sum(x => x.Debit),
                TotalCredit = items.Sum(x => x.Credit),
                Items = items
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForPurchaseInvoiceAsync(PurchaseInvoice invoice, CancellationToken ct = default)
        {
            // در سیستم ثبت دائمی موجودی انبار (Perpetual Inventory)، خرید کالا ابتدا به حساب دارایی انبار (1105) بدهکار می‌شود
            var accInventory = await GetSystemAccountAsync(invoice.TenantId, ACC_INVENTORY, ct);
            var accVatRec = await GetSystemAccountAsync(invoice.TenantId, ACC_VAT_RECEIVABLE, ct);
            var accPayable = await GetSystemAccountAsync(invoice.TenantId, ACC_PAYABLE, ct);
            var accDiscount = await GetSystemAccountAsync(invoice.TenantId, ACC_PURCHASE_DISCOUNT, ct);

            var items = new List<JournalVoucherItem>();
            int row = 1;

            items.Add(new JournalVoucherItem
            {
                CreatedAt = DateTime.UtcNow,
                TenantId = invoice.TenantId,
                RowNumber = row++,
                AccountId = accInventory.Id,
                Description = $"خرید کالا طبق فاکتور خرید شماره {invoice.Number}",
                Debit = invoice.Subtotal,
                Credit = 0,
                SupplierId = invoice.SupplierId,
                WarehouseId = invoice.WarehouseId
            });

            if (invoice.Tax > 0)
            {
                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    CreatedAt = DateTime.UtcNow,
                    AccountId = accVatRec.Id,
                    Description = $"مالیات ارزش افزوده فاکتور خرید شماره {invoice.Number}",
                    Debit = invoice.Tax,
                    Credit = 0,
                    SupplierId = invoice.SupplierId
                });
            }

            items.Add(new JournalVoucherItem
            {
                TenantId = invoice.TenantId,
                CreatedAt = DateTime.UtcNow,
                RowNumber = row++,
                AccountId = accPayable.Id,
                Description = $"بدهی به تأمین‌کننده بابت فاکتور خرید شماره {invoice.Number}",
                Debit = 0,
                Credit = invoice.Total,
                SupplierId = invoice.SupplierId
            });

            if (invoice.Discount > 0)
            {
                items.Add(new JournalVoucherItem
                {
                    TenantId = invoice.TenantId,
                    RowNumber = row++,
                    AccountId = accDiscount.Id,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"تخفیف فاکتور خرید شماره {invoice.Number}",
                    Debit = 0,
                    Credit = invoice.Discount,
                    SupplierId = invoice.SupplierId
                });
            }

            var nextNumber = await GetNextVoucherNumberAsync(invoice.TenantId, invoice.FiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = invoice.TenantId,
                FiscalYearId = invoice.FiscalYearId,
                VoucherNumber = nextNumber,
                CreatedAt = DateTime.UtcNow,
                VoucherDate = invoice.IssueDate,
                Description = $"سند خودکار فاکتور خرید شماره {invoice.Number}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.PurchaseInvoice,
                ReferenceId = invoice.Id,
                TotalDebit = items.Sum(x => x.Debit),
                TotalCredit = items.Sum(x => x.Credit),
                Items = items
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForReceiptAsync(Receipt receipt, CancellationToken ct = default)
        {
            var accCashBank = await GetSystemAccountAsync(receipt.TenantId, ACC_CASH_BANK, ct);
            var accReceivable = await GetSystemAccountAsync(receipt.TenantId, ACC_RECEIVABLE, ct);
            var bankAcc = await ResolveBankAccountAsync(receipt.TenantId, receipt.BankAccountId, ct);
            var bankTitle = bankAcc != null ? $"{bankAcc.Title} - {bankAcc.BankName}" : "خزانه/بانک";

            var nextNumber = await GetNextVoucherNumberAsync(receipt.TenantId, receipt.FiscalYearId, ct);
            var refDesc = !string.IsNullOrWhiteSpace(receipt.ReferenceNo) ? $" (شماره پیگیری: {receipt.ReferenceNo})" : "";

            var voucher = new JournalVoucher
            {
                TenantId = receipt.TenantId,
                FiscalYearId = receipt.FiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = receipt.ReceiptDate,
                Description = $"سند دریافت وجه به {bankTitle} ({receipt.Method}){refDesc}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.Receipt,
                ReferenceId = receipt.Id,
                TotalDebit = receipt.Amount,
                TotalCredit = receipt.Amount,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = receipt.TenantId,
                        RowNumber = 1,
                        AccountId = accCashBank.Id,
                        Description = $"افزایش موجودی {bankTitle} بابت دریافت شماره {receipt.Id}{refDesc}",
                        Debit = receipt.Amount,
                        Credit = 0,
                        CreatedAt = DateTime.UtcNow,
                        CustomerId = receipt.CustomerId,
                        BankAccountId = bankAcc?.Id ?? receipt.BankAccountId
                    },
                    new()
                    {
                        TenantId = receipt.TenantId,
                        RowNumber = 2,
                        AccountId = accReceivable.Id,
                        Description = $"تصفیه حساب دریافتنی مشتری بابت دریافت شماره {receipt.Id}",
                        Debit = 0,CreatedAt = DateTime.UtcNow,
                        Credit = receipt.Amount,
                        CustomerId = receipt.CustomerId
                    }
                }
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForPaymentAsync(Payment payment, CancellationToken ct = default)
        {
            var accPayable = await GetSystemAccountAsync(payment.TenantId, ACC_PAYABLE, ct);
            var accCashBank = await GetSystemAccountAsync(payment.TenantId, ACC_CASH_BANK, ct);

            int? supplierId = null;
            int? projectId = null;

            if (payment.PurchaseInvoiceId.HasValue)
            {
                var pInv = await _context.Set<PurchaseInvoice>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == payment.PurchaseInvoiceId.Value, ct);
                supplierId = pInv?.SupplierId;
            }
            else if (payment.ExpenseId.HasValue)
            {
                var exp = await _context.Set<Expense>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == payment.ExpenseId.Value, ct);
                projectId = exp?.ProjectId;
            }

            var bankAcc = await ResolveBankAccountAsync(payment.TenantId, payment.BankAccountId, ct);
            var bankTitle = bankAcc != null ? $"{bankAcc.Title} - {bankAcc.BankName}" : "خزانه/بانک";

            var nextNumber = await GetNextVoucherNumberAsync(payment.TenantId, payment.FiscalYearId, ct);
            var refDesc = !string.IsNullOrWhiteSpace(payment.ReferenceNo) ? $" (پیگیری: {payment.ReferenceNo})" : "";

            var voucher = new JournalVoucher
            {
                TenantId = payment.TenantId,
                FiscalYearId = payment.FiscalYearId,
                VoucherNumber = nextNumber,
                CreatedAt = DateTime.UtcNow,
                VoucherDate = payment.PaymentDate,
                Description = $"سند پرداخت وجه از {bankTitle} ({payment.Method}){refDesc}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.Payment,
                ReferenceId = payment.Id,
                TotalDebit = payment.Amount,
                TotalCredit = payment.Amount,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = payment.TenantId,
                        RowNumber = 1,
                        AccountId = accPayable.Id,
                        Description = $"تسویه بدهی بابت سند پرداخت شماره {payment.Id}",
                        Debit = payment.Amount,
                        Credit = 0,
                        CreatedAt = DateTime.UtcNow,
                        SupplierId = supplierId,
                        ProjectId = projectId
                    },
                    new()
                    {
                        TenantId = payment.TenantId,
                        RowNumber = 2,
                        AccountId = accCashBank.Id,
                        Description = $"کسر از {bankTitle} بابت پرداخت شماره {payment.Id}{refDesc}",
                        Debit = 0,
                        Credit = payment.Amount,
                        CreatedAt = DateTime.UtcNow,
                        SupplierId = supplierId,
                        ProjectId = projectId,
                        BankAccountId = bankAcc?.Id ?? payment.BankAccountId
                    }
                }
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForExpenseAsync(Expense expense, CancellationToken ct = default)
        {
            var accExpense = await GetSystemAccountAsync(expense.TenantId, ACC_PROJECT_EXPENSE, ct);
            var accPayable = await GetSystemAccountAsync(expense.TenantId, ACC_PAYABLE, ct);

            var nextNumber = await GetNextVoucherNumberAsync(expense.TenantId, expense.FiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = expense.TenantId,
                FiscalYearId = expense.FiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = expense.ExpenseDate,
                Description = $"شناسایی هزینه: {expense.ExpenseTitle}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.Expense,
                ReferenceId = expense.Id,
                TotalDebit = expense.Amount,
                TotalCredit = expense.Amount,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = expense.TenantId,
                        RowNumber = 1,
                        AccountId = accExpense.Id,
                        Description = $"ثبت هزینه پروژه: {expense.ExpenseTitle}",
                        Debit = expense.Amount,
                        Credit = 0,CreatedAt = DateTime.UtcNow,
                        ProjectId = expense.ProjectId
                    },
                    new()
                    {
                        TenantId = expense.TenantId,
                        RowNumber = 2,
                        AccountId = accPayable.Id,
                        Description = $"ایجاد تعهد پرداخت بابت هزینه: {expense.ExpenseTitle}",
                        Debit = 0,
                        Credit = expense.Amount,CreatedAt = DateTime.UtcNow,
                        ProjectId = expense.ProjectId
                    }
                }
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task CancelVoucherByReferenceAsync(AccountingReferenceType referenceType, int referenceId, string? reason = null, CancellationToken ct = default)
        {
            var voucher = await _context.JournalVouchers
                .FirstOrDefaultAsync(x => x.ReferenceType == referenceType && x.ReferenceId == referenceId && x.Status != JournalVoucherStatus.Cancelled, ct);

            if (voucher == null) return;

            voucher.Status = JournalVoucherStatus.Cancelled;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                voucher.Description += $" | علت ابطال: {reason.Trim()}";
            }

            _unitOfWork.journalVoucherUW.Update(voucher, ct);
            await _unitOfWork.SaveAsync();
        }

        public async Task<List<TrialBalanceRowDto>> GetTrialBalanceAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        {
            var query = _context.JournalVoucherItems
                .AsNoTracking()
                .Include(x => x.Account)
                .Include(x => x.JournalVoucher)
                .Where(x => x.JournalVoucher.Status == JournalVoucherStatus.Posted && !x.JournalVoucher.IsDeleted);

            if (fromDate.HasValue)
                query = query.Where(x => x.JournalVoucher.VoucherDate >= fromDate.Value.Date);

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.JournalVoucher.VoucherDate <= end);
            }

            return await query
                .GroupBy(x => new { x.AccountId, x.Account.Code, x.Account.Title, x.Account.Category })
                .Select(g => new TrialBalanceRowDto
                {
                    AccountId = g.Key.AccountId,
                    AccountCode = g.Key.Code,
                    AccountTitle = g.Key.Title,
                    CategoryTitle = g.Key.Category.ToString(),
                    TurnoverDebit = g.Sum(i => i.Debit),
                    TurnoverCredit = g.Sum(i => i.Credit)
                })
                .OrderBy(x => x.AccountCode)
                .ToListAsync(ct);
        }

        public async Task<List<LedgerTransactionDto>> GetLedgerAsync(
            int? accountId = null,
            int? customerId = null,
            int? supplierId = null,
            int? projectId = null,
            int? bankAccountId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var query = _context.JournalVoucherItems
                .AsNoTracking()
                .Include(x => x.Account)
                .Include(x => x.BankAccount)
                .Include(x => x.JournalVoucher)
                .Where(x => x.JournalVoucher.Status == JournalVoucherStatus.Posted && !x.JournalVoucher.IsDeleted);

            if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
            if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
            if (bankAccountId.HasValue) query = query.Where(x => x.BankAccountId == bankAccountId.Value);
            if (fromDate.HasValue) query = query.Where(x => x.JournalVoucher.VoucherDate >= fromDate.Value.Date);
            if (toDate.HasValue) query = query.Where(x => x.JournalVoucher.VoucherDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            var rawItems = await query
                .OrderBy(x => x.JournalVoucher.VoucherDate)
                .ThenBy(x => x.JournalVoucher.VoucherNumber)
                .ThenBy(x => x.RowNumber)
                .ToListAsync(ct);

            decimal running = 0m;
            var result = new List<LedgerTransactionDto>();

            foreach (var item in rawItems)
            {
                running += (item.Debit - item.Credit);
                result.Add(new LedgerTransactionDto
                {
                    VoucherId = item.JournalVoucherId,
                    VoucherNumber = item.JournalVoucher.VoucherNumber,
                    VoucherDate = item.JournalVoucher.VoucherDate,
                    AccountCode = item.Account.Code,
                    AccountTitle = item.Account.Title,
                    Description = item.Description,
                    ReferenceTypeTitle = item.JournalVoucher.ReferenceType.ToString(),
                    ReferenceId = item.JournalVoucher.ReferenceId,
                    BankAccountId = item.BankAccountId,
                    BankAccountTitle = item.BankAccount != null ? $"{item.BankAccount.Title} ({item.BankAccount.BankName})" : null,
                    Debit = item.Debit,
                    Credit = item.Credit,
                    RunningBalance = running
                });
            }

            return result;
        }

        public async Task<JournalVoucher?> CreateOpeningVoucherForBankAccountAsync(BankAccount bankAccount, CancellationToken ct = default)
        {
            if (bankAccount.InitialBalance <= 0) return null;

            var fiscalYearId = _currentFiscalYear.FiscalYearId
                ?? throw new InvalidOperationException("سال مالی فعال برای صدور سند موجودی اولیه بانک یافت نشد.");

            var accCashBank = await GetSystemAccountAsync(bankAccount.TenantId, ACC_CASH_BANK, ct);
            var accEquity = await GetSystemAccountAsync(bankAccount.TenantId, ACC_EQUITY, ct);
            var nextNumber = await GetNextVoucherNumberAsync(bankAccount.TenantId, fiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = bankAccount.TenantId,
                FiscalYearId = fiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = DateTime.UtcNow,
                Description = $"سند موجودی اول دوره حساب بانکی/صندوق ({bankAccount.Title} - {bankAccount.BankName})",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.OpeningBalance,
                ReferenceId = bankAccount.Id,
                TotalDebit = bankAccount.InitialBalance,
                TotalCredit = bankAccount.InitialBalance,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = bankAccount.TenantId,
                        RowNumber = 1,
                        CreatedAt = DateTime.UtcNow,
                        AccountId = accCashBank.Id,
                        BankAccountId = bankAccount.Id,
                        Description = $"موجودی اول دوره {bankAccount.Title} ({bankAccount.BankName})",
                        Debit = bankAccount.InitialBalance,
                        Credit = 0
                    },
                    new()
                    {
                        TenantId = bankAccount.TenantId,
                        RowNumber = 2,
                        CreatedAt = DateTime.UtcNow,
                        AccountId = accEquity.Id,
                        Description = $"طرف حساب موجودی اول دوره {bankAccount.Title}",
                        Debit = 0,
                        Credit = bankAccount.InitialBalance
                    }
                }
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<List<BankAccountBalanceSummaryDto>> GetBankAccountsBalanceSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        {
            var bankAccounts = await _context.Set<BankAccount>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            var itemsQuery = _context.JournalVoucherItems
                .AsNoTracking()
                .Include(x => x.JournalVoucher)
                .Where(x => x.BankAccountId != null
                         && x.JournalVoucher.Status == JournalVoucherStatus.Posted
                         && !x.JournalVoucher.IsDeleted);

            if (fromDate.HasValue)
                itemsQuery = itemsQuery.Where(x => x.JournalVoucher.VoucherDate >= fromDate.Value.Date);
            if (toDate.HasValue)
                itemsQuery = itemsQuery.Where(x => x.JournalVoucher.VoucherDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

            var groupedMovements = await itemsQuery
                .GroupBy(x => x.BankAccountId!.Value)
                .Select(g => new
                {
                    BankAccountId = g.Key,
                    TotalDeposits = g.Sum(i => i.Debit),
                    TotalWithdrawals = g.Sum(i => i.Credit)
                })
                .ToDictionaryAsync(x => x.BankAccountId, ct);

            return bankAccounts.Select(ba =>
            {
                groupedMovements.TryGetValue(ba.Id, out var mov);
                return new BankAccountBalanceSummaryDto
                {
                    BankAccountId = ba.Id,
                    Title = ba.Title,
                    BankName = ba.BankName,
                    AccountNumber = ba.AccountNumber,
                    ShebaNumber = ba.ShebaNumber,
                    CardNumber = ba.CardNumber,
                    IsDefault = ba.IsDefault,
                    IsActive = ba.IsActive,
                    TotalDeposits = mov?.TotalDeposits ?? 0m,
                    TotalWithdrawals = mov?.TotalWithdrawals ?? 0m
                };
            }).ToList();
        }

        public async Task<ProfitAndLossReportDto> GetProfitAndLossAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default)
        {
            var trialBalance = await GetTrialBalanceAsync(fromDate, toDate, ct);

            var revenues = trialBalance.Where(x => x.AccountCode.StartsWith("4")).ToList();
            var costsAndExpenses = trialBalance.Where(x => x.AccountCode.StartsWith("5")).ToList();

            var totalRevenue = revenues.Sum(x => x.TurnoverCredit - x.TurnoverDebit);
            var totalPurchaseCost = costsAndExpenses.Where(x => x.AccountCode.StartsWith("51")).Sum(x => x.TurnoverDebit - x.TurnoverCredit);
            var totalOperatingExpense = costsAndExpenses.Where(x => x.AccountCode.StartsWith("52")).Sum(x => x.TurnoverDebit - x.TurnoverCredit);

            return new ProfitAndLossReportDto
            {
                TotalSalesRevenue = totalRevenue,
                TotalPurchasesAndCost = totalPurchaseCost,
                TotalOperatingExpenses = totalOperatingExpense,
                RevenueAccounts = revenues,
                ExpenseAccounts = costsAndExpenses
            };
        }

        /// <summary>
        /// عملیات کامل بستن سال مالی (بستن حساب‌های موقت، انتقال سود/زیان به سرمایه، صدور سند اختتامیه و صدور سند افتتاحیه سال جدید)
        /// </summary>
        public async Task<CloseFiscalYearResultDto> CloseFiscalYearAndGenerateOpeningAsync(CloseFiscalYearRequestModel model, CancellationToken ct = default)
        {
            var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("شناسه شرکت یافت نشد.");
            var sourceFiscalYearId = model.SourceFiscalYearId ?? _currentFiscalYear.FiscalYearId
                ?? throw new InvalidOperationException("سال مالی مبدأ مشخص نشده است.");

            var sourceFiscalYear = await _context.FiscalYears
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == sourceFiscalYearId && f.TenantId == tenantId && !f.IsDeleted, ct);

            if (sourceFiscalYear == null)
                throw new InvalidOperationException("سال مالی مورد نظر یافت نشد.");

            if (sourceFiscalYear.IsClosed)
                throw new InvalidOperationException($"سال مالی {sourceFiscalYear.Title} قبلاً بسته شده است.");

            var nextStartDate = ConvertDate.ConvertShamsiToMiladi(model.NextStartDateShamsi);
            var nextEndDate = ConvertDate.ConvertShamsiToMiladi(model.NextEndDateShamsi);

            using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
            try
            {
                await EnsureDefaultChartOfAccountsAsync(tenantId, ct);
                var accEquity = await GetSystemAccountAsync(tenantId, ACC_EQUITY, ct);

                // ۱. استخراج تمام آرتیکل‌های قطعی در سال مالی مبدأ (بدون تداخل Global Query Filter)
                var allPostedItems = await _context.JournalVoucherItems
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(x => x.Account)
                    .Include(x => x.JournalVoucher)
                    .Where(x => x.TenantId == tenantId
                             && !x.IsDeleted
                             && x.JournalVoucher.FiscalYearId == sourceFiscalYearId
                             && x.JournalVoucher.Status == JournalVoucherStatus.Posted
                             && !x.JournalVoucher.IsDeleted)
                    .ToListAsync(ct);

                // ۲. گروه‌بندی حساب‌های موقت (درآمد: Revenue و هزینه: Expense)
                var tempBalances = allPostedItems
                    .Where(x => x.Account.Category == AccountCategory.Revenue || x.Account.Category == AccountCategory.Expense)
                    .GroupBy(x => new { x.AccountId, x.Account.Code, x.Account.Title })
                    .Select(g => new
                    {
                        g.Key.AccountId,
                        g.Key.Code,
                        g.Key.Title,
                        NetBalance = g.Sum(i => i.Debit - i.Credit) // مثبت یعنی مانده بدهکار، منفی یعنی مانده بستانکار
                    })
                    .Where(x => x.NetBalance != 0)
                    .ToList();

                int? pnlVoucherNumber = null;
                decimal netProfitOrLoss = 0m;

                // ۳. صدور سند بستن حساب‌های موقت (درآمد و هزینه) به حساب سرمایه و سود انباشته (3101)
                if (tempBalances.Any())
                {
                    var pnlItems = new List<JournalVoucherItem>();
                    int row = 1;

                    foreach (var tb in tempBalances)
                    {
                        if (tb.NetBalance < 0)
                        {
                            // حساب دارای مانده بستانکار است (مثل فروش 4101) -> برای بستن باید بدهکار شود
                            pnlItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = row++,
                                AccountId = tb.AccountId,
                                Description = $"بستن حساب موقت ({tb.Title}) در پایان سال مالی {sourceFiscalYear.Title}",
                                Debit = Math.Abs(tb.NetBalance),
                                CreatedAt = DateTime.UtcNow,
                                Credit = 0
                            });
                        }
                        else if (tb.NetBalance > 0)
                        {
                            // حساب دارای مانده بدهکار است (مثل خرید 5101 یا هزینه پروژه 5201) -> برای بستن باید بستانکار شود
                            pnlItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = row++,
                                AccountId = tb.AccountId,
                                Description = $"بستن حساب موقت ({tb.Title}) در پایان سال مالی {sourceFiscalYear.Title}",
                                Debit = 0,
                                CreatedAt = DateTime.UtcNow,
                                Credit = tb.NetBalance
                            });
                        }
                    }

                    var sumDebit = pnlItems.Sum(x => x.Debit);
                    var sumCredit = pnlItems.Sum(x => x.Credit);
                    netProfitOrLoss = sumDebit - sumCredit; // اگر مثبت باشد یعنی درآمدها بیشتر بوده (سود خالص)، اگر منفی یعنی زیان

                    if (netProfitOrLoss > 0)
                    {
                        // سود خالص دوره -> بستانکار شدن حساب سرمایه و سود انباشته (3101)
                        pnlItems.Add(new JournalVoucherItem
                        {
                            TenantId = tenantId,
                            RowNumber = row++,
                            AccountId = accEquity.Id,
                            Description = $"انتقال سود خالص سال مالی {sourceFiscalYear.Title} به سود انباشته",
                            Debit = 0,
                            CreatedAt = DateTime.UtcNow,
                            Credit = netProfitOrLoss
                        });
                    }
                    else if (netProfitOrLoss < 0)
                    {
                        // زیان خالص دوره -> بدهکار شدن حساب سرمایه و سود انباشته (3101)
                        pnlItems.Add(new JournalVoucherItem
                        {
                            TenantId = tenantId,
                            RowNumber = row++,
                            AccountId = accEquity.Id,
                            Description = $"انتقال زیان خالص سال مالی {sourceFiscalYear.Title} به سود انباشته",
                            Debit = Math.Abs(netProfitOrLoss),
                            CreatedAt = DateTime.UtcNow,
                            Credit = 0
                        });
                    }

                    pnlVoucherNumber = await GetNextVoucherNumberAsync(tenantId, sourceFiscalYearId, ct);
                    var pnlVoucher = new JournalVoucher
                    {
                        TenantId = tenantId,
                        FiscalYearId = sourceFiscalYearId,
                        VoucherNumber = pnlVoucherNumber.Value,
                        VoucherDate = sourceFiscalYear.EndDate,
                        Description = $"سند بستن حساب‌های موقت (سود و زیان) سال مالی {sourceFiscalYear.Title}",
                        Status = JournalVoucherStatus.Posted,
                        ReferenceType = AccountingReferenceType.ClosingBalance,
                        TotalDebit = pnlItems.Sum(x => x.Debit),
                        TotalCredit = pnlItems.Sum(x => x.Credit),
                        CreatedAt = DateTime.UtcNow,
                        Items = pnlItems
                    };

                    await _context.JournalVouchers.AddAsync(pnlVoucher, ct);
                    await _context.SaveChangesAsync(ct);
                }

                // ۴. محاسبه مانده حساب‌های دائم (دارایی، بدهی، سرمایه) به تفکیک تفصیلی‌های شناور (Customer, Supplier, Project, Warehouse)
                // شامل سود/زیانی که در مرحله ۳ به حساب 3101 منتقل شد
                var permanentItemsAfterPnl = await _context.JournalVoucherItems
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Include(x => x.Account)
                    .Include(x => x.JournalVoucher)
                    .Where(x => x.TenantId == tenantId
                             && !x.IsDeleted
                             && x.JournalVoucher.FiscalYearId == sourceFiscalYearId
                             && x.JournalVoucher.Status == JournalVoucherStatus.Posted
                             && !x.JournalVoucher.IsDeleted
                             && (x.Account.Category == AccountCategory.Asset
                              || x.Account.Category == AccountCategory.Liability
                              || x.Account.Category == AccountCategory.Equity))
                    .ToListAsync(ct);

                var permanentBalances = permanentItemsAfterPnl
                    .GroupBy(x => new
                    {
                        x.AccountId,
                        x.Account.Title,
                        x.CustomerId,
                        x.SupplierId,
                        x.ProjectId,
                        x.WarehouseId,
                        x.BankAccountId
                    })
                    .Select(g => new
                    {
                        g.Key.AccountId,
                        g.Key.Title,
                        g.Key.CustomerId,
                        g.Key.SupplierId,
                        g.Key.ProjectId,
                        g.Key.WarehouseId,
                        g.Key.BankAccountId,
                        NetBalance = g.Sum(i => i.Debit - i.Credit)
                    })
                    .Where(x => x.NetBalance != 0)
                    .ToList();

                // ۵. ایجاد یا بازیابی سال مالی جدید
                var nextFiscalYear = await _context.FiscalYears
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Title == model.NextFiscalYearTitle.Trim() && !f.IsDeleted, ct);

                if (nextFiscalYear == null)
                {
                    nextFiscalYear = new FiscalYear
                    {
                        TenantId = tenantId,
                        Title = model.NextFiscalYearTitle.Trim(),
                        StartDate = nextStartDate,
                        EndDate = nextEndDate,
                        IsClosed = false,
                        IsCurrent = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = _currentUser.UserId
                    };
                    await _context.FiscalYears.AddAsync(nextFiscalYear, ct);
                    await _context.SaveChangesAsync(ct);
                }

                int? closingVoucherNumber = null;
                int? openingVoucherNumber = null;
                decimal totalPermanentBalance = 0m;

                // ۶. صدور سند اختتامیه در سال قدیم و سند افتتاحیه در سال جدید
                if (permanentBalances.Any())
                {
                    var closingItems = new List<JournalVoucherItem>();
                    var openingItems = new List<JournalVoucherItem>();
                    int closeRow = 1;
                    int openRow = 1;

                    foreach (var pb in permanentBalances)
                    {
                        if (pb.NetBalance > 0)
                        {
                            // حساب دارای مانده بدهکار است (مثل بانک یا حساب دریافتنی مشتری):
                            // در سند اختتامیه بستانکار می‌شود و در سند افتتاحیه سال جدید بدهکار می‌شود
                            closingItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = closeRow++,
                                AccountId = pb.AccountId,
                                Description = $"سند اختتامیه حساب ({pb.Title}) - انتقال به سال {nextFiscalYear.Title}",
                                Debit = 0,
                                Credit = pb.NetBalance,
                                CustomerId = pb.CustomerId,
                                SupplierId = pb.SupplierId,
                                ProjectId = pb.ProjectId,
                                CreatedAt = DateTime.UtcNow,
                                WarehouseId = pb.WarehouseId,
                                BankAccountId = pb.BankAccountId
                            });

                            openingItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = openRow++,
                                AccountId = pb.AccountId,
                                Description = $"سند افتتاحیه حساب ({pb.Title}) - انتقالی از سال {sourceFiscalYear.Title}",
                                Debit = pb.NetBalance,
                                Credit = 0,
                                CustomerId = pb.CustomerId,
                                SupplierId = pb.SupplierId,
                                ProjectId = pb.ProjectId,
                                WarehouseId = pb.WarehouseId,
                                CreatedAt = DateTime.UtcNow,
                                BankAccountId = pb.BankAccountId
                            });
                        }
                        else if (pb.NetBalance < 0)
                        {
                            var absBal = Math.Abs(pb.NetBalance);
                            // حساب دارای مانده بستانکار است (مثل حساب پرداختنی تأمین‌کننده یا سرمایه):
                            // در سند اختتامیه بدهکار می‌شود و در سند افتتاحیه سال جدید بستانکار می‌شود
                            closingItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = closeRow++,
                                AccountId = pb.AccountId,
                                Description = $"سند اختتامیه حساب ({pb.Title}) - انتقال به سال {nextFiscalYear.Title}",
                                Debit = absBal,
                                Credit = 0,
                                CustomerId = pb.CustomerId,
                                SupplierId = pb.SupplierId,
                                ProjectId = pb.ProjectId,
                                CreatedAt = DateTime.UtcNow,
                                WarehouseId = pb.WarehouseId,
                                BankAccountId = pb.BankAccountId
                            });

                            openingItems.Add(new JournalVoucherItem
                            {
                                TenantId = tenantId,
                                RowNumber = openRow++,
                                AccountId = pb.AccountId,
                                Description = $"سند افتتاحیه حساب ({pb.Title}) - انتقالی از سال {sourceFiscalYear.Title}",
                                Debit = 0,
                                Credit = absBal,
                                CustomerId = pb.CustomerId,
                                SupplierId = pb.SupplierId,
                                ProjectId = pb.ProjectId,
                                CreatedAt = DateTime.UtcNow,
                                WarehouseId = pb.WarehouseId,
                                BankAccountId = pb.BankAccountId
                            });
                        }
                    }

                    totalPermanentBalance = openingItems.Sum(x => x.Debit);

                    closingVoucherNumber = await GetNextVoucherNumberAsync(tenantId, sourceFiscalYearId, ct);
                    var closingVoucher = new JournalVoucher
                    {
                        TenantId = tenantId,
                        FiscalYearId = sourceFiscalYearId,
                        VoucherNumber = closingVoucherNumber.Value,
                        VoucherDate = sourceFiscalYear.EndDate,
                        Description = $"سند اختتامیه سال مالی {sourceFiscalYear.Title}",
                        Status = JournalVoucherStatus.Posted,
                        ReferenceType = AccountingReferenceType.ClosingBalance,
                        TotalDebit = closingItems.Sum(x => x.Debit),
                        TotalCredit = closingItems.Sum(x => x.Credit),
                        CreatedAt = DateTime.UtcNow,
                        Items = closingItems
                    };

                    openingVoucherNumber = await GetNextVoucherNumberAsync(tenantId, nextFiscalYear.Id, ct);
                    var openingVoucher = new JournalVoucher
                    {
                        TenantId = tenantId,
                        FiscalYearId = nextFiscalYear.Id,
                        VoucherNumber = openingVoucherNumber.Value,
                        VoucherDate = nextFiscalYear.StartDate,
                        Description = $"سند افتتاحیه سال مالی {nextFiscalYear.Title} (انتقالی از سال {sourceFiscalYear.Title})",
                        Status = JournalVoucherStatus.Posted,
                        ReferenceType = AccountingReferenceType.OpeningBalance,
                        TotalDebit = openingItems.Sum(x => x.Debit),
                        CreatedAt = DateTime.UtcNow,
                        TotalCredit = openingItems.Sum(x => x.Credit),
                        Items = openingItems
                    };

                    await _context.JournalVouchers.AddRangeAsync(new[] { closingVoucher, openingVoucher }, ct);
                }

                // ۷. بستن سال مالی قدیم و فعال کردن سال مالی جدید
                sourceFiscalYear.IsClosed = true;
                sourceFiscalYear.IsCurrent = false;
                nextFiscalYear.IsCurrent = true;

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return new CloseFiscalYearResultDto
                {
                    ClosedFiscalYearId = sourceFiscalYear.Id,
                    ClosedFiscalYearTitle = sourceFiscalYear.Title,
                    NewFiscalYearId = nextFiscalYear.Id,
                    NewFiscalYearTitle = nextFiscalYear.Title,
                    NetProfitOrLossTransferred = netProfitOrLoss,
                    ProfitLossClosingVoucherNumber = pnlVoucherNumber,
                    ClosingVoucherNumber = closingVoucherNumber,
                    OpeningVoucherNumberInNewYear = openingVoucherNumber,
                    TotalPermanentBalanceTransferred = totalPermanentBalance
                };
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        // =========================================================================
        // ۱۰. مدیریت چک‌های دریافتنی و پرداختنی (واگذاری به بانک، وصول، برگشت، خرج و راس‌گیری)
        // =========================================================================

        public async Task<JournalVoucher> CreateVoucherForChequeRegistrationAsync(Cheque cheque, CancellationToken ct = default)
        {
            var tenantId = cheque.TenantId > 0
                ? cheque.TenantId
                : (_currentUser.TenantId ?? throw new UnauthorizedAccessException("شناسه شرکت یافت نشد."));

            var fiscalYearId = cheque.FiscalYearId > 0
                ? cheque.FiscalYearId
                : (_currentFiscalYear.FiscalYearId ?? throw new InvalidOperationException("سال مالی فعال یافت نشد."));

            var nextNumber = await GetNextVoucherNumberAsync(tenantId, fiscalYearId, ct);
            var items = new List<JournalVoucherItem>();

            if (cheque.Type == ChequeType.Receivable)
            {
                var accNotesRec = await GetSystemAccountAsync(tenantId, ACC_NOTES_RECEIVABLE, ct);
                var accCustomerRec = await GetSystemAccountAsync(tenantId, ACC_RECEIVABLE, ct);

                // ۱. بدهکار شدن اسناد دریافتنی نزد صندوق (1102)
                items.Add(new JournalVoucherItem
                {
                    TenantId = tenantId,
                    RowNumber = 1,
                    AccountId = accNotesRec.Id,
                    Description = $"دریافت چک شماره {cheque.ChequeNumber} ({cheque.BankName}) سررسید {ConvertDate.ConvertMiladiToShamsi(cheque.DueDate, "yyyy/MM/dd")}",
                    Debit = cheque.Amount,
                    Credit = 0,
                    CustomerId = cheque.CustomerId,
                    ProjectId = cheque.ProjectId,
                    CreatedAt = DateTime.UtcNow
                });

                // ۲. بستانکار شدن حساب دریافتنی مشتری (1103)
                items.Add(new JournalVoucherItem
                {
                    TenantId = tenantId,
                    RowNumber = 2,
                    AccountId = accCustomerRec.Id,
                    Description = $"بابت دریافت چک شماره {cheque.ChequeNumber} صیادی {cheque.SayadiId ?? "-"} از مشتری",
                    Debit = 0,
                    Credit = cheque.Amount,
                    CustomerId = cheque.CustomerId,
                    ProjectId = cheque.ProjectId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                var accSupplierPay = await GetSystemAccountAsync(tenantId, ACC_PAYABLE, ct);
                var accNotesPay = await GetSystemAccountAsync(tenantId, ACC_NOTES_PAYABLE, ct);
                var bankAcc = await ResolveBankAccountAsync(tenantId, cheque.BankAccountId, ct);

                // ۱. بدهکار شدن حساب پرداختنی تأمین‌کننده (2101)
                items.Add(new JournalVoucherItem
                {
                    TenantId = tenantId,
                    RowNumber = 1,
                    AccountId = accSupplierPay.Id,
                    Description = $"صدور و تحویل چک پرداختنی شماره {cheque.ChequeNumber} سررسید {ConvertDate.ConvertMiladiToShamsi(cheque.DueDate, "yyyy/MM/dd")}",
                    Debit = cheque.Amount,
                    Credit = 0,
                    SupplierId = cheque.SupplierId,
                    ProjectId = cheque.ProjectId,
                    CreatedAt = DateTime.UtcNow
                });

                // ۲. بستانکار شدن اسناد پرداختنی تجاری (2103)
                items.Add(new JournalVoucherItem
                {
                    TenantId = tenantId,
                    RowNumber = 2,
                    AccountId = accNotesPay.Id,
                    Description = $"اسناد پرداختنی بابت چک شماره {cheque.ChequeNumber} عهده ({bankAcc?.Title ?? cheque.BankName})",
                    Debit = 0,
                    Credit = cheque.Amount,
                    SupplierId = cheque.SupplierId,
                    ProjectId = cheque.ProjectId,
                    BankAccountId = bankAcc?.Id ?? cheque.BankAccountId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var voucher = new JournalVoucher
            {
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                VoucherNumber = nextNumber,
                VoucherDate = cheque.IssueDate,
                Description = cheque.Type == ChequeType.Receivable
                    ? $"سند خودکار دریافت چک شماره {cheque.ChequeNumber} نزد صندوق"
                    : $"سند خودکار صدور چک پرداختنی شماره {cheque.ChequeNumber}",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.ChequeOperation,
                ReferenceId = cheque.Id,
                TotalDebit = cheque.Amount,
                TotalCredit = cheque.Amount,
                CreatedAt = DateTime.UtcNow,
                Items = items
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> ChangeChequeStatusAndCreateVoucherAsync(ChequeStatusChangeModel model, CancellationToken ct = default)
        {
            var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("شناسه شرکت یافت نشد.");
            var fiscalYearId = _currentFiscalYear.FiscalYearId ?? throw new InvalidOperationException("سال مالی فعال یافت نشد.");

            var cheque = await _context.Set<Cheque>()
                .FirstOrDefaultAsync(x => x.Id == model.ChequeId && x.TenantId == tenantId && !x.IsDeleted, ct)
                ?? throw new InvalidOperationException("چک مورد نظر یافت نشد.");

            if (cheque.Status == model.NewStatus)
                throw new InvalidOperationException("وضعیت جدید چک با وضعیت فعلی آن یکسان است.");

            var previousStatus = cheque.Status;
            var actionDate = !string.IsNullOrWhiteSpace(model.ActionDateShamsi)
                ? ConvertDate.ConvertShamsiToMiladi(model.ActionDateShamsi)
                : DateTime.UtcNow;

            var accCashBank = await GetSystemAccountAsync(tenantId, ACC_CASH_BANK, ct);
            var accNotesRec = await GetSystemAccountAsync(tenantId, ACC_NOTES_RECEIVABLE, ct);
            var accNotesInCol = await GetSystemAccountAsync(tenantId, ACC_NOTES_IN_COLLECTION, ct);
            var accReceivable = await GetSystemAccountAsync(tenantId, ACC_RECEIVABLE, ct);
            var accPayable = await GetSystemAccountAsync(tenantId, ACC_PAYABLE, ct);
            var accNotesPay = await GetSystemAccountAsync(tenantId, ACC_NOTES_PAYABLE, ct);

            var items = new List<JournalVoucherItem>();
            string voucherTitle;

            if (cheque.Type == ChequeType.Receivable)
            {
                if (model.NewStatus == ChequeStatus.InBankForCollection)
                {
                    // واگذاری چک دریافتنی به بانک جهت وصول (کلکسیون)
                    var targetBank = await ResolveBankAccountAsync(tenantId, model.TargetBankAccountId ?? cheque.BankAccountId, ct)
                        ?? throw new InvalidOperationException("لطفاً حساب بانکی مقصد جهت واگذاری چک را مشخص کنید.");

                    cheque.BankAccountId = targetBank.Id;
                    voucherTitle = $"سند خودکار واگذاری چک دریافتنی {cheque.ChequeNumber} به ({targetBank.Title}) جهت وصول";

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accNotesInCol.Id,
                        Description = $"واگذاری چک شماره {cheque.ChequeNumber} به ({targetBank.Title}) در جریان وصول",
                        Debit = cheque.Amount,
                        Credit = 0,
                        CustomerId = cheque.CustomerId,
                        BankAccountId = targetBank.Id,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = accNotesRec.Id,
                        Description = $"خروج چک شماره {cheque.ChequeNumber} از صندوق جهت واگذاری به بانک",
                        Debit = 0,
                        Credit = cheque.Amount,
                        CustomerId = cheque.CustomerId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (model.NewStatus == ChequeStatus.Cleared)
                {
                    // وصول چک دریافتنی و واریز به حساب بانکی شرکت
                    var targetBank = await ResolveBankAccountAsync(tenantId, model.TargetBankAccountId ?? cheque.BankAccountId, ct);
                    if (targetBank != null)
                        cheque.BankAccountId = targetBank.Id;

                    var creditAccount = previousStatus == ChequeStatus.InBankForCollection ? accNotesInCol : accNotesRec;
                    voucherTitle = $"سند خودکار وصول چک دریافتنی {cheque.ChequeNumber} و واریز به ({targetBank?.Title ?? "بانک شرکت"})";

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accCashBank.Id,
                        Description = $"وصول چک دریافتنی شماره {cheque.ChequeNumber} و واریز به ({targetBank?.Title ?? "خزانه"})",
                        Debit = cheque.Amount,
                        Credit = 0,
                        CustomerId = cheque.CustomerId,
                        BankAccountId = targetBank?.Id,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = creditAccount.Id,
                        Description = $"تسویه اسناد دریافتنی بابت وصول چک شماره {cheque.ChequeNumber}",
                        Debit = 0,
                        Credit = cheque.Amount,
                        CustomerId = cheque.CustomerId,
                        BankAccountId = previousStatus == ChequeStatus.InBankForCollection ? targetBank?.Id : null,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (model.NewStatus == ChequeStatus.Bounced)
                {
                    // برگشت خوردن چک دریافتنی (بدهکار شدن مجدد مشتری)
                    var creditAccount = previousStatus == ChequeStatus.InBankForCollection ? accNotesInCol : accNotesRec;
                    voucherTitle = $"سند خودکار برگشت چک دریافتنی شماره {cheque.ChequeNumber} و بدهکار شدن مشتری";

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accReceivable.Id,
                        Description = $"برگشت چک شماره {cheque.ChequeNumber} ({cheque.BankName}) و انتقال به حساب بدهکاری مشتری",
                        Debit = cheque.Amount,
                        Credit = 0,
                        CustomerId = cheque.CustomerId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = creditAccount.Id,
                        Description = $"خروج چک برگشتی شماره {cheque.ChequeNumber} از حساب اسناد دریافتنی",
                        Debit = 0,
                        Credit = cheque.Amount,
                        CustomerId = cheque.CustomerId,
                        BankAccountId = previousStatus == ChequeStatus.InBankForCollection ? cheque.BankAccountId : null,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (model.NewStatus == ChequeStatus.EndorsedToSupplier)
                {
                    // خرج کردن چک دریافتنی به تأمین‌کننده
                    var targetSupplierId = model.EndorsedToSupplierId ?? cheque.EndorsedToSupplierId
                        ?? throw new InvalidOperationException("لطفاً تأمین‌کننده گیرنده چک (خرج چک) را مشخص نمایید.");

                    cheque.EndorsedToSupplierId = targetSupplierId;
                    voucherTitle = $"سند خودکار خرج چک دریافتنی شماره {cheque.ChequeNumber} به تأمین‌کننده";

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accPayable.Id,
                        Description = $"واگذاری (خرج) چک دریافتنی شماره {cheque.ChequeNumber} بابت تسویه با تأمین‌کننده",
                        Debit = cheque.Amount,
                        Credit = 0,
                        SupplierId = targetSupplierId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = accNotesRec.Id,
                        Description = $"خروج چک شماره {cheque.ChequeNumber} از صندوق بابت خرج به تأمین‌کننده",
                        Debit = 0,
                        Credit = cheque.Amount,
                        CustomerId = cheque.CustomerId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    throw new InvalidOperationException("تغییر وضعیت درخواستی برای چک دریافتنی معتبر نیست.");
                }
            }
            else
            {
                // چک پرداختنی شرکت (Payable)
                if (model.NewStatus == ChequeStatus.Cleared)
                {
                    var sourceBank = await ResolveBankAccountAsync(tenantId, model.TargetBankAccountId ?? cheque.BankAccountId, ct);
                    if (sourceBank != null)
                        cheque.BankAccountId = sourceBank.Id;

                    voucherTitle = $"سند خودکار پاس شدن چک پرداختنی شماره {cheque.ChequeNumber} از ({sourceBank?.Title ?? cheque.BankName})";

                    // ۱. بدهکار شدن اسناد پرداختنی تجاری (2103)
                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accNotesPay.Id,
                        Description = $"تسویه اسناد پرداختنی بابت پاس شدن چک شماره {cheque.ChequeNumber}",
                        Debit = cheque.Amount,
                        Credit = 0,
                        SupplierId = cheque.SupplierId,
                        BankAccountId = sourceBank?.Id,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    // ۲. بستانکار شدن موجودی نقد و بانک شرکت (1101)
                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = accCashBank.Id,
                        Description = $"کسر از حساب ({sourceBank?.Title ?? cheque.BankName}) بابت پاس شدن چک پرداختنی {cheque.ChequeNumber}",
                        Debit = 0,
                        Credit = cheque.Amount,
                        SupplierId = cheque.SupplierId,
                        BankAccountId = sourceBank?.Id,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else if (model.NewStatus == ChequeStatus.Returned || model.NewStatus == ChequeStatus.Bounced)
                {
                    voucherTitle = $"سند خودکار ابطال / برگشت چک پرداختنی شماره {cheque.ChequeNumber}";

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 1,
                        AccountId = accNotesPay.Id,
                        Description = $"ابطال/عودت چک پرداختنی شماره {cheque.ChequeNumber}",
                        Debit = cheque.Amount,
                        Credit = 0,
                        SupplierId = cheque.SupplierId,
                        BankAccountId = cheque.BankAccountId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });

                    items.Add(new JournalVoucherItem
                    {
                        TenantId = tenantId,
                        RowNumber = 2,
                        AccountId = accPayable.Id,
                        Description = $"برگشت بدهی به حساب تأمین‌کننده بابت ابطال/برگشت چک {cheque.ChequeNumber}",
                        Debit = 0,
                        Credit = cheque.Amount,
                        SupplierId = cheque.SupplierId,
                        ProjectId = cheque.ProjectId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    throw new InvalidOperationException("تغییر وضعیت درخواستی برای چک پرداختنی معتبر نیست.");
                }
            }

            cheque.Status = model.NewStatus;
            cheque.StatusChangedAt = actionDate;
            if (!string.IsNullOrWhiteSpace(model.Description))
                cheque.Description = model.Description.Trim();

            _unitOfWork.chequeUW.Update(cheque);

            var nextNumber = await GetNextVoucherNumberAsync(tenantId, fiscalYearId, ct);
            var voucher = new JournalVoucher
            {
                TenantId = tenantId,
                FiscalYearId = fiscalYearId,
                VoucherNumber = nextNumber,
                VoucherDate = actionDate,
                Description = voucherTitle,
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.ChequeOperation,
                ReferenceId = cheque.Id,
                TotalDebit = cheque.Amount,
                TotalCredit = cheque.Amount,
                CreatedAt = DateTime.UtcNow,
                Items = items
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();

            return voucher;
        }

        public async Task<ChequeRasGiriResultDto> CalculateChequesRasGiriAsync(ChequeRasGiriRequestModel model, CancellationToken ct = default)
        {
            var tenantId = _currentUser.TenantId ?? throw new UnauthorizedAccessException("شناسه شرکت یافت نشد.");

            var baseDate = !string.IsNullOrWhiteSpace(model.BaseDateShamsi)
                ? ConvertDate.ConvertShamsiToMiladi(model.BaseDateShamsi).Date
                : DateTime.UtcNow.Date;

            var query = _context.Set<Cheque>()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted);

            if (model.ChequeIds != null && model.ChequeIds.Count > 0)
            {
                query = query.Where(x => model.ChequeIds.Contains(x.Id));
            }
            else
            {
                if (model.Type.HasValue)
                    query = query.Where(x => x.Type == model.Type.Value);

                if (model.CustomerId.HasValue)
                    query = query.Where(x => x.CustomerId == model.CustomerId.Value);

                if (model.SupplierId.HasValue)
                    query = query.Where(x => x.SupplierId == model.SupplierId.Value);
            }

            var cheques = await query
                .OrderBy(x => x.DueDate)
                .ToListAsync(ct);

            if (cheques.Count == 0)
                throw new InvalidOperationException("هیچ چکی جهت محاسبه راس‌گیری یافت نشد.");

            var items = new List<ChequeRasGiriItemDto>();
            decimal totalAmount = 0m;
            decimal totalWeightedAmountDays = 0m;

            foreach (var chq in cheques)
            {
                var daysDiff = (int)Math.Round((chq.DueDate.Date - baseDate).TotalDays);
                var weighted = chq.Amount * daysDiff;

                totalAmount += chq.Amount;
                totalWeightedAmountDays += weighted;

                items.Add(new ChequeRasGiriItemDto
                {
                    ChequeId = chq.Id,
                    ChequeNumber = chq.ChequeNumber,
                    SayadiId = chq.SayadiId,
                    BankName = chq.BankName,
                    Amount = chq.Amount,
                    DueDate = chq.DueDate,
                    DueDateShamsi = ConvertDate.ConvertMiladiToShamsi(chq.DueDate, "yyyy/MM/dd"),
                    DaysFromBaseDate = daysDiff,
                    WeightedAmountDays = weighted
                });
            }

            var weightedAvgDays = totalAmount > 0
                ? Math.Round(totalWeightedAmountDays / totalAmount, 2)
                : 0m;

            var exactRasDate = baseDate.AddDays((double)Math.Round(weightedAvgDays));
            var discountRate = model.MonthlyDiscountRatePercent > 0 ? model.MonthlyDiscountRatePercent : 0m;
            var estimatedDiscount = discountRate > 0 && weightedAvgDays > 0
                ? Math.Round(totalAmount * (discountRate / 100m) * (weightedAvgDays / 30m), 0)
                : 0m;

            return new ChequeRasGiriResultDto
            {
                ChequeCount = cheques.Count,
                TotalAmount = totalAmount,
                BaseDate = baseDate,
                BaseDateShamsi = ConvertDate.ConvertMiladiToShamsi(baseDate, "yyyy/MM/dd"),
                TotalWeightedAmountDays = totalWeightedAmountDays,
                WeightedAverageDays = weightedAvgDays,
                ExactRasDate = exactRasDate,
                ExactRasDateShamsi = ConvertDate.ConvertMiladiToShamsi(exactRasDate, "yyyy/MM/dd"),
                MonthlyDiscountRatePercent = discountRate,
                EstimatedDiscountAmount = estimatedDiscount,
                Items = items
            };
        }
    }
}