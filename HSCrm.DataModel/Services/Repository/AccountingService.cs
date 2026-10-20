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
        public const string ACC_RECEIVABLE = "1103";         // حساب‌های دریافتنی تجاری (مشتریان)
        public const string ACC_INVENTORY = "1105";          // موجودی مواد و کالا (انبار)
        public const string ACC_VAT_RECEIVABLE = "1106";     // مالیات بر ارزش افزوده خرید
        public const string ACC_PAYABLE = "2101";            // حساب‌های پرداختنی تجاری (تأمین‌کنندگان)
        public const string ACC_VAT_PAYABLE = "2102";        // مالیات بر ارزش افزوده فروش
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
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_RECEIVABLE, Title = "حساب‌های دریافتنی تجاری (مشتریان و پروژه‌ها)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.Both, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_INVENTORY, Title = "موجودی کالا و انبار", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_VAT_RECEIVABLE, Title = "پیش‌پرداخت مالیات ارزش افزوده (خرید)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Asset, Nature = AccountNature.DebitOnly, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_PAYABLE, Title = "حساب‌های پرداختنی تجاری (تأمین‌کنندگان و هزینه‌ها)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Liability, Nature = AccountNature.Both, IsSystemAccount = true },
                new() {CreatedAt = DateTime.UtcNow, TenantId = tenantId, Code = ACC_VAT_PAYABLE, Title = "مالیات بر ارزش افزوده فروش (پرداختنی)", Level = AccountLevel.Subsidiary, Category = AccountCategory.Liability, Nature = AccountNature.CreditOnly, IsSystemAccount = true },
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
                throw new InvalidOperationException($"حساب سیستمی با کد {code} یافت نشد.");

            return account;
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
                    WarehouseId = item.WarehouseId
                }).ToList()
            };

            await _unitOfWork.journalVoucherUW.CreateAsync(voucher, ct);
            await _unitOfWork.SaveAsync();
            return voucher;
        }

        public async Task<JournalVoucher> CreateVoucherForSalesInvoiceAsync(SalesInvoice invoice, CancellationToken ct = default)
        {
            var accReceivable = await GetSystemAccountAsync(invoice.TenantId, ACC_RECEIVABLE, ct);
            var accSales = await GetSystemAccountAsync(invoice.TenantId, ACC_SALES_REVENUE, ct);
            var accDiscount = await GetSystemAccountAsync(invoice.TenantId, ACC_SALES_DISCOUNT, ct);
            var accVat = await GetSystemAccountAsync(invoice.TenantId, ACC_VAT_PAYABLE, ct);

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

            var nextNumber = await GetNextVoucherNumberAsync(invoice.TenantId, invoice.FiscalYearId, ct);

            var voucher = new JournalVoucher
            {
                TenantId = invoice.TenantId,
                FiscalYearId = invoice.FiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = invoice.IssueDate,
                Description = $"سند خودکار فاکتور فروش شماره {invoice.Number}",
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
            var accInventory = await GetSystemAccountAsync(invoice.TenantId, ACC_PURCHASE_COST, ct);
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

            var nextNumber = await GetNextVoucherNumberAsync(receipt.TenantId, receipt.FiscalYearId, ct);
            var refDesc = !string.IsNullOrWhiteSpace(receipt.ReferenceNo) ? $" (شماره پیگیری: {receipt.ReferenceNo})" : "";

            var voucher = new JournalVoucher
            {
                TenantId = receipt.TenantId,
                FiscalYearId = receipt.FiscalYearId,
                CreatedAt = DateTime.UtcNow,
                VoucherNumber = nextNumber,
                VoucherDate = receipt.ReceiptDate,
                Description = $"سند دریافت وجه ({receipt.Method}){refDesc}",
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
                        Description = $"افزایش موجودی خزانه/بانک بابت دریافت شماره {receipt.Id}{refDesc}",
                        Debit = receipt.Amount,
                        Credit = 0,CreatedAt = DateTime.UtcNow,
                        CustomerId = receipt.CustomerId
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

            var nextNumber = await GetNextVoucherNumberAsync(payment.TenantId, payment.FiscalYearId, ct);
            var refDesc = !string.IsNullOrWhiteSpace(payment.ReferenceNo) ? $" (پیگیری: {payment.ReferenceNo})" : "";

            var voucher = new JournalVoucher
            {
                TenantId = payment.TenantId,
                FiscalYearId = payment.FiscalYearId,
                VoucherNumber = nextNumber,
                CreatedAt = DateTime.UtcNow,
                VoucherDate = payment.PaymentDate,
                Description = $"سند پرداخت وجه ({payment.Method}){refDesc}",
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
                        SupplierId = supplierId,CreatedAt = DateTime.UtcNow,
                        ProjectId = projectId
                    },
                    new()
                    {
                        TenantId = payment.TenantId,
                        RowNumber = 2,
                        AccountId = accCashBank.Id,
                        Description = $"کسر از خزانه/بانک بابت پرداخت شماره {payment.Id}{refDesc}",
                        Debit = 0,
                        Credit = payment.Amount,CreatedAt = DateTime.UtcNow,
                        SupplierId = supplierId,
                        ProjectId = projectId
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
            DateTime? fromDate = null,
            DateTime? toDate = null,
            CancellationToken ct = default)
        {
            var query = _context.JournalVoucherItems
                .AsNoTracking()
                .Include(x => x.Account)
                .Include(x => x.JournalVoucher)
                .Where(x => x.JournalVoucher.Status == JournalVoucherStatus.Posted && !x.JournalVoucher.IsDeleted);

            if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
            if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId.Value);
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
                    Debit = item.Debit,
                    Credit = item.Credit,
                    RunningBalance = running
                });
            }

            return result;
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
    }
}