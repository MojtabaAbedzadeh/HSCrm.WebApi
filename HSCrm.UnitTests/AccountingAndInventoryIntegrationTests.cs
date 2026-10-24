using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class MutableCurrentUserService : ICurrentUserService
    {
        public string? UserId { get; set; } = "test-user-1";
        public int? TenantId { get; set; } = 1;
        public bool IsAuthenticated { get; set; } = true;
    }

    public class MutableCurrentFiscalYearService : ICurrentFiscalYearService
    {
        public int? FiscalYearId { get; set; } = 1;
    }

    public class AccountingAndInventoryIntegrationTests
    {
        private static (
            ApplicationDbContext Context,
            UnitOfWork Uow,
            AccountingService Accounting,
            InventoryService Inventory,
            MutableCurrentUserService CurrentUser,
            MutableCurrentFiscalYearService CurrentFiscalYear
        ) CreateTestRig(string dbName)
        {
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = "user-admin", IsAuthenticated = true };
            var currentFy = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + "_" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options, currentUser, currentFy);

            // Seed Tenant, FiscalYear, Warehouse, Customer, Supplier, Project, Product
            context.Tenants.Add(new Tenant
            {
                Id = 1,
                TenantCode = "TNT01",
                TenantTitle = "شرکت مهندسی آسانسور هگمتانه",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.FiscalYears.Add(new FiscalYear
            {
                Id = 1,
                TenantId = 1,
                Title = "سال مالی 1404",
                StartDate = new DateTime(2025, 3, 21),
                EndDate = new DateTime(2026, 3, 20),
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });

            context.Warehouses.Add(new Warehouse
            {
                Id = 1,
                TenantId = 1,
                Title = "انبار مرکزی قطعات",
                Address = "تهران - شهرک صنعتی",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.Customers.Add(new Customer
            {
                Id = 1,
                TenantId = 1,
                Name = "مجتمع تجاری البرز",
                Phone = "09121112233",
                Address = "تهران - سعادت‌آباد",
                CreatedAt = DateTime.UtcNow
            });

            context.Projects.Add(new Project
            {
                Id = 1,
                TenantId = 1,
                CustomerId = 1,
                ProjectTitle = "پروژه نصب ۲ دستگاه آسانسور برج البرز",
                ProjectLocation = "سعادت‌آباد",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });

            context.Suppliers.Add(new Supplier
            {
                Id = 1,
                TenantId = 1,
                SupplierFullName = "شرکت بازرگانی موتور بهران",
                SupplierPhone = "02188776655",
                SupplierAddress = "تهران - خیابان سعدی",
                CreatedAt = DateTime.UtcNow
            });

            context.Products.Add(new Product
            {
                Id = 1,
                TenantId = 1,
                ProductTitle = "موتور گیرلس آسانسور 11KW",
                ProductUnit = "عدد",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.SaveChanges();

            var uow = new UnitOfWork(context);
            var accounting = new AccountingService(context, uow, currentUser, currentFy);
            var inventory = new InventoryService(context, uow, currentFy, currentUser);

            return (context, uow, accounting, inventory, currentUser, currentFy);
        }

        [Fact(DisplayName = "01. ایجاد خودکار و بدون تکرار ۲۰ سرفصل استاندارد حسابداری (EnsureDefaultChartOfAccounts)")]
        public async Task Test01_EnsureDefaultChartOfAccounts_SeedsAll20StandardAccounts_Idempotently()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test01_EnsureDefaultChartOfAccounts_SeedsAll20StandardAccounts_Idempotently));

            await accounting.EnsureDefaultChartOfAccountsAsync(1);
            await accounting.EnsureDefaultChartOfAccountsAsync(1); // فراخوانی مجدد برای تست Idempotency

            var accounts = await context.AccountingAccounts.Where(a => a.TenantId == 1 && !a.IsDeleted).ToListAsync();
            Assert.Equal(16, accounts.Count);
            Assert.Contains(accounts, a => a.Code == "1101" && a.Category == AccountCategory.Asset);
            Assert.Contains(accounts, a => a.Code == "1105" && a.Title.Contains("موجودی کالا"));
            Assert.Contains(accounts, a => a.Code == "2101" && a.Category == AccountCategory.Liability);
            Assert.Contains(accounts, a => a.Code == "3101" && a.Category == AccountCategory.Equity);            
            Assert.Contains(accounts, a => a.Code == "4103" && a.Title.Contains("برگشت از فروش"));
            Assert.Contains(accounts, a => a.Code == "5101" && a.Category == AccountCategory.Expense);
        }

        [Fact(DisplayName = "02. کنترل تراز بودن سند روزنامه دستی (رد سند ناتراز و ثبت موفق سند تراز)")]
        public async Task Test02_ManualJournalVoucher_BalancedPostsSuccessfully_UnbalancedThrowsException()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test02_ManualJournalVoucher_BalancedPostsSuccessfully_UnbalancedThrowsException));
            await accounting.EnsureDefaultChartOfAccountsAsync(1);

            var accBank = await context.AccountingAccounts.FirstAsync(a => a.Code == "1101");
            var accEquity = await context.AccountingAccounts.FirstAsync(a => a.Code == "3101");

            // ۱. سند ناتراز باید خطا بدهد
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                accounting.CreateManualVoucherAsync(new JournalVoucherCreateModel
                {
                    VoucherDateShamsi = "1404/01/15",
                    Description = "سند ناتراز آزمایشی",
                    Items = new List<JournalVoucherItemCreateModel>
                    {
                        new() { AccountId = accBank.Id, Debit = 100_000_000m, Credit = 0m, Description = "بدهکار" },
                        new() { AccountId = accEquity.Id, Debit = 0m, Credit = 90_000_000m, Description = "بستانکار ناتراز" }
                    }
                }));

            // ۲. سند تراز باید با موفقیت ثبت شود
            var voucher = await accounting.CreateManualVoucherAsync(new JournalVoucherCreateModel
            {
                VoucherDateShamsi = "1404/01/15",
                Description = "آورده اولیه نقدی شرکا",
                PostImmediately = true,
                Items = new List<JournalVoucherItemCreateModel>
                {
                    new() { AccountId = accBank.Id, Debit = 100_000_000m, Credit = 0m, Description = "واریز به بانک" },
                    new() { AccountId = accEquity.Id, Debit = 0m, Credit = 100_000_000m, Description = "سرمایه اولیه" }
                }
            });

            Assert.Equal(1, voucher.VoucherNumber);
            Assert.Equal(100_000_000m, voucher.TotalDebit);
            Assert.Equal(100_000_000m, voucher.TotalCredit);

            var tb = await accounting.GetTrialBalanceAsync();
            Assert.Equal(100_000_000m, tb.Sum(x => x.TurnoverDebit));
            Assert.Equal(100_000_000m, tb.Sum(x => x.TurnoverCredit));
        }

        [Fact(DisplayName = "03. صدور خودکار سند افتتاحیه حساب بانکی و گزارش موجودی لحظه‌ای خزانه")]
        public async Task Test03_BankAccountOpeningBalance_CreatesBalancedVoucherWithCapital3101()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test03_BankAccountOpeningBalance_CreatesBalancedVoucherWithCapital3101));

            var bank = new BankAccount
            {
                TenantId = 1,
                Title = "بانک ملت شعبه مرکزی",
                BankName = "بانک ملت",
                AccountNumber = "123456789",
                InitialBalance = 500_000_000m,
                IsDefault = true,
                IsActive = true
            };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();

            var voucher = await accounting.CreateOpeningVoucherForBankAccountAsync(bank);
            Assert.NotNull(voucher);
            Assert.Equal(500_000_000m, voucher!.TotalDebit);
            Assert.Equal(500_000_000m, voucher.TotalCredit);

            var summaries = await accounting.GetBankAccountsBalanceSummaryAsync();
            Assert.Single(summaries);
            Assert.Equal(500_000_000m, summaries[0].CurrentBalance);
        }

        [Fact(DisplayName = "04. فاکتور خرید: محاسبه میانگین موزون متحرک، لایه‌های FIFO و سند حسابداری خرید")]
        public async Task Test04_PurchaseInvoice_UpdatesMovingWeightedAverageAndFifoLayers_AndPostsBalancedVoucher()
        {
            var (context, _, accounting, inventory, _, _) = CreateTestRig(nameof(Test04_PurchaseInvoice_UpdatesMovingWeightedAverageAndFifoLayers_AndPostsBalancedVoucher));

            // خرید پارت ۱: ۱۰ عدد با نرخ ۱,۰۰۰,۰۰۰ ریال
            var pi1 = new PurchaseInvoice
            {
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                PurchaserUserId = "user-admin",
                Number = "PI-1001",
                IssueDate = new DateTime(2025, 4, 1),
                Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m,
                Discount = 0m,
                Tax = 0m,
                Total = 10_000_000m,
                Description = "خرید پارت اول موتور",
                Items = new List<PurchaseInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 1_000_000m, LineTotal = 10_000_000m }
                }
            };
            context.PurchaseInvoices.Add(pi1);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi1);
            await accounting.CreateVoucherForPurchaseInvoiceAsync(pi1);
            await context.SaveChangesAsync();

            // خرید پارت ۲: ۱۰ عدد با نرخ ۲,۰۰۰,۰۰۰ ریال + ۱,۸۰۰,۰۰۰ ریال ارزش افزوده
            var pi2 = new PurchaseInvoice
            {
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                PurchaserUserId = "user-admin",
                Number = "PI-1002",
                IssueDate = new DateTime(2025, 4, 5),
                Status = InvoiceStatus.Final,
                Subtotal = 20_000_000m,
                Discount = 0m,
                Tax = 1_800_000m,
                Total = 21_800_000m,
                Description = "خرید پارت دوم موتور",
                Items = new List<PurchaseInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 2_000_000m, Tax = 1_800_000m, LineTotal = 21_800_000m }
                }
            };
            context.PurchaseInvoices.Add(pi2);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi2);
            var voucher2 = await accounting.CreateVoucherForPurchaseInvoiceAsync(pi2);
            await context.SaveChangesAsync();

            var inv = await context.Inventories.FirstAsync(x => x.ProductId == 1 && x.WarehouseId == 1);
            Assert.Equal(20m, inv.Quantity);
            Assert.Equal(1_500_000m, inv.AverageUnitCost); // (10M + 20M) / 20 = 1.5M

            Assert.Equal(21_800_000m, voucher2.TotalDebit);
            Assert.Equal(21_800_000m, voucher2.TotalCredit);
        }

        [Fact(DisplayName = "05. فاکتور فروش (میانگین موزون): محاسبه خودکار بهای تمام‌شده (COGS) و ثبت سند ۴ آرتیکلی")]
        public async Task Test05_SalesInvoice_WeightedAverageCosting_CalculatesCogsAndGrossProfit()
        {
            var (context, _, accounting, inventory, _, _) = CreateTestRig(nameof(Test05_SalesInvoice_WeightedAverageCosting_CalculatesCogsAndGrossProfit));

            // ورود ۱۰ عدد @ ۱,۰۰۰,۰۰۰ و ۱۰ عدد @ ۲,۰۰۰,۰۰۰ (میانگین موزون = ۱,۵۰۰,۰۰۰)
            var pi1 = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-1", IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m, Total = 10_000_000m, Description = "پارت ۱",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 1_000_000m, LineTotal = 10_000_000m } }
            };
            var pi2 = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-2", IssueDate = new DateTime(2025, 4, 2), Status = InvoiceStatus.Final,
                Subtotal = 20_000_000m, Total = 20_000_000m, Description = "پارت ۲",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 2_000_000m, LineTotal = 20_000_000m } }
            };
            context.PurchaseInvoices.AddRange(pi1, pi2);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi1);
            await inventory.ApplyInventoryForPurchaseInvoice(pi2);
            await context.SaveChangesAsync();

            // فروش ۸ عدد با قیمت فروش ۲,۵۰۰,۰۰۰ ریال به روش میانگین موزون
            var sale = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SellerUserId = "u1",
                Number = "SI-1001", IssueDate = new DateTime(2025, 4, 10), Status = InvoiceStatus.Final,
                Subtotal = 20_000_000m, Tax = 1_800_000m, Total = 21_800_000m, Description = "فروش ۸ دستگاه موتور",
                Items = new List<SalesInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 8, UnitPriceSell = 2_500_000m, Tax = 1_800_000m, LineTotal = 21_800_000m }
                }
            };
            context.SalesInvoices.Add(sale);
            await context.SaveChangesAsync();

            inventory.ApplyInventoryForSalesInvoice(sale, InventoryCostingMethod.WeightedAverage);
            var salesVoucher = await accounting.CreateVoucherForSalesInvoiceAsync(sale, InventoryCostingMethod.WeightedAverage);
            await context.SaveChangesAsync();

            var saleItem = sale.Items.First();
            Assert.Equal(1_500_000m, saleItem.UnitCost);
            Assert.Equal(12_000_000m, saleItem.TotalCost); // 8 * 1,500,000 = 12,000,000

            // سند فروش شامل ۲۱,۸۰۰,۰۰۰ درآمد/مالیات + ۱۲,۰۰۰,۰۰۰ بهای تمام‌شده = ۳۳,۸۰۰,۰۰۰ ریال تراز
            Assert.Equal(33_800_000m, salesVoucher.TotalDebit);
            Assert.Equal(33_800_000m, salesVoucher.TotalCredit);

            var pnl = await accounting.GetProfitAndLossAsync();
            Assert.Equal(20_000_000m, pnl.TotalSalesRevenue);
            Assert.Equal(12_000_000m, pnl.TotalPurchasesAndCost);
            Assert.Equal(8_000_000m, pnl.GrossProfit);
        }

        [Fact(DisplayName = "06. فاکتور فروش (FIFO): مصرف از قدیمی‌ترین پارت‌های خرید و گزارش ارزش‌گذاری مقایسه‌ای انبار")]
        public async Task Test06_SalesInvoice_FifoCosting_ConsumesOldestLayersFirst_AndValuationReportMatches()
        {
            var (context, _, accounting, inventory, _, _) = CreateTestRig(nameof(Test06_SalesInvoice_FifoCosting_ConsumesOldestLayersFirst_AndValuationReportMatches));

            var pi1 = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-1", IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m, Total = 10_000_000m, Description = "پارت ۱",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 1_000_000m, LineTotal = 10_000_000m } }
            };
            var pi2 = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-2", IssueDate = new DateTime(2025, 4, 2), Status = InvoiceStatus.Final,
                Subtotal = 20_000_000m, Total = 20_000_000m, Description = "پارت ۲",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 2_000_000m, LineTotal = 20_000_000m } }
            };
            context.PurchaseInvoices.AddRange(pi1, pi2);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi1);
            await inventory.ApplyInventoryForPurchaseInvoice(pi2);
            await context.SaveChangesAsync();

            // فروش ۱۲ عدد به روش FIFO -> ۱۰ عدد از پارت ۱ (۱۰ میلیون) + ۲ عدد از پارت ۲ (۴ میلیون) = ۱۴,۰۰۰,۰۰۰ ریال
            var sale = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SellerUserId = "u1",
                Number = "SI-FIFO", IssueDate = new DateTime(2025, 4, 12), Status = InvoiceStatus.Final,
                Subtotal = 30_000_000m, Total = 30_000_000m, Description = "فروش FIFO",
                Items = new List<SalesInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 12, UnitPriceSell = 2_500_000m, LineTotal = 30_000_000m }
                }
            };
            context.SalesInvoices.Add(sale);
            await context.SaveChangesAsync();

            inventory.ApplyInventoryForSalesInvoice(sale, InventoryCostingMethod.FIFO);
            await accounting.CreateVoucherForSalesInvoiceAsync(sale, InventoryCostingMethod.FIFO);
            await context.SaveChangesAsync();

            Assert.Equal(14_000_000m, sale.Items.First().TotalCost);

            // بررسی گزارش ارزش‌گذاری انبار: ۸ عدد باقی‌مانده از پارت ۲ به ارزش FIFO معادل ۱۶,۰۰۰,۰۰۰ ریال و میانگین موزون ۱۲,۰۰۰,۰۰۰ ریال
            var valuation = await inventory.GetInventoryValuationAsync(1);
            Assert.Single(valuation);
            Assert.Equal(8m, valuation[0].CurrentQuantity);
            Assert.Equal(16_000_000m, valuation[0].FifoTotalValue);
            Assert.Equal(12_000_000m, valuation[0].WeightedAverageTotalValue);
        }

        [Fact(DisplayName = "07. فاکتور برگشت از فروش: بازگشت کالا به انبار و صدور سند معکوس فروش و COGS")]
        public async Task Test07_SalesReturnInvoice_RestoresInventoryAndReversesRevenueVatAndCogs()
        {
            var (context, _, accounting, inventory, _, _) = CreateTestRig(nameof(Test07_SalesReturnInvoice_RestoresInventoryAndReversesRevenueVatAndCogs));

            var pi = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-1", IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m, Total = 10_000_000m, Description = "خرید اولیه",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 1_000_000m, LineTotal = 10_000_000m } }
            };
            context.PurchaseInvoices.Add(pi);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi);
            await accounting.CreateVoucherForPurchaseInvoiceAsync(pi);

            var sale = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SellerUserId = "u1",
                Number = "SI-1", IssueDate = new DateTime(2025, 4, 5), Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m, Total = 10_000_000m, Description = "فروش ۵ عدد",
                Items = new List<SalesInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 5, UnitPriceSell = 2_000_000m, LineTotal = 10_000_000m } }
            };
            context.SalesInvoices.Add(sale);
            await context.SaveChangesAsync();
            inventory.ApplyInventoryForSalesInvoice(sale, InventoryCostingMethod.WeightedAverage);
            await accounting.CreateVoucherForSalesInvoiceAsync(sale, InventoryCostingMethod.WeightedAverage);
            await context.SaveChangesAsync();

            // برگشت ۲ عدد از فاکتور فروش SI-1
            var salesReturn = new SalesReturnInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SalesInvoiceId = sale.Id, SellerUserId = "u1",
                Number = "SR-1", IssueDate = new DateTime(2025, 4, 8), Status = InvoiceStatus.Final,
                Subtotal = 4_000_000m, Total = 4_000_000m, Description = "برگشت ۲ عدد موتور",
                Items = new List<SalesReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 2, UnitPriceReturn = 2_000_000m, LineTotal = 4_000_000m }
                }
            };
            context.SalesReturnInvoices.Add(salesReturn);
            await context.SaveChangesAsync();

            await inventory.ApplyInventoryForSalesReturnAsync(salesReturn);
            var returnVoucher = await accounting.CreateVoucherForSalesReturnAsync(salesReturn);
            await context.SaveChangesAsync();

            var inv = await context.Inventories.FirstAsync(x => x.ProductId == 1);
            Assert.Equal(7m, inv.Quantity); // 10 - 5 + 2 = 7
            Assert.Equal(1_000_000m, salesReturn.Items.First().UnitCost);
            Assert.Equal(2_000_000m, salesReturn.Items.First().TotalCost);
            Assert.Equal(6_000_000m, returnVoucher.TotalDebit); // 4M برگشت فروش + 2M برگشت COGS
            Assert.Equal(6_000_000m, returnVoucher.TotalCredit);
        }

        [Fact(DisplayName = "08. فاکتور برگشت از خرید: خروج کالا از انبار و کسر بدهی تأمین‌کننده")]
        public async Task Test08_PurchaseReturnInvoice_DeductsInventoryAndReversesPayableAndInputVat()
        {
            var (context, _, accounting, inventory, _, _) = CreateTestRig(nameof(Test08_PurchaseReturnInvoice_DeductsInventoryAndReversesPayableAndInputVat));

            var pi = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaserUserId = "u1",
                Number = "PI-1", IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m, Total = 10_000_000m, Description = "خرید ۱۰ عدد",
                Items = new List<PurchaseInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 10, UnitPricePurchase = 1_000_000m, LineTotal = 10_000_000m } }
            };
            context.PurchaseInvoices.Add(pi);
            await context.SaveChangesAsync();
            await inventory.ApplyInventoryForPurchaseInvoice(pi);
            await accounting.CreateVoucherForPurchaseInvoiceAsync(pi);
            await context.SaveChangesAsync();

            var purchaseReturn = new PurchaseReturnInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1, PurchaseInvoiceId = pi.Id, PurchaserUserId = "u1",
                Number = "PR-1", IssueDate = new DateTime(2025, 4, 6), Status = InvoiceStatus.Final,
                Subtotal = 3_000_000m, Total = 3_000_000m, Description = "برگشت ۳ عدد معیوب به تأمین‌کننده",
                Items = new List<PurchaseReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 3, UnitPriceReturn = 1_000_000m, LineTotal = 3_000_000m }
                }
            };
            context.PurchaseReturnInvoices.Add(purchaseReturn);
            await context.SaveChangesAsync();

            await inventory.ApplyInventoryForPurchaseReturnAsync(purchaseReturn);
            var prVoucher = await accounting.CreateVoucherForPurchaseReturnAsync(purchaseReturn);
            await context.SaveChangesAsync();

            var inv = await context.Inventories.FirstAsync(x => x.ProductId == 1);
            Assert.Equal(7m, inv.Quantity); // 10 - 3 = 7
            Assert.Equal(3_000_000m, prVoucher.TotalDebit);
            Assert.Equal(3_000_000m, prVoucher.TotalCredit);
        }

        [Fact(DisplayName = "09. اسناد دریافت و پرداخت خزانه‌داری و کنترل مانده تفصیلی بانک")]
        public async Task Test09_TreasuryReceiptAndPayment_UpdateBankAccountBalanceAndPartyLedger()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test09_TreasuryReceiptAndPayment_UpdateBankAccountBalanceAndPartyLedger));

            var bank = new BankAccount
            {
                TenantId = 1, Title = "بانک سامان", BankName = "سامان", InitialBalance = 100_000_000m, IsDefault = true, IsActive = true
            };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();
            await accounting.CreateOpeningVoucherForBankAccountAsync(bank);

            var receipt = new Receipt
            {
                TenantId = 1, FiscalYearId = 1, CustomerId = 1, BankAccountId = bank.Id,
                ReceiptDate = new DateTime(2025, 4, 15), Amount = 45_000_000m, Method = "POS", Description = "دریافت قسط اول از مشتری"
            };
            context.Receipts.Add(receipt);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForReceiptAsync(receipt);

            var payment = new Payment
            {
                TenantId = 1, FiscalYearId = 1, BankAccountId = bank.Id,
                PaymentDate = new DateTime(2025, 4, 16), Amount = 25_000_000m, Method = "Havaleh", Description = "پرداخت علی‌الحساب"
            };
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForPaymentAsync(payment);

            var summary = await accounting.GetBankAccountsBalanceSummaryAsync();
            Assert.Single(summary);
            Assert.Equal(145_000_000m, summary[0].TotalDeposits);
            Assert.Equal(25_000_000m, summary[0].TotalWithdrawals);
            Assert.Equal(120_000_000m, summary[0].CurrentBalance);
        }

        [Fact(DisplayName = "10. ثبت هزینه پروژه (تفکیک سرفصل 5201 تا 5299) و ابطال خودکار سند در زمان حذف")]
        public async Task Test10_ProjectExpense_MapsExpenseTypeToSpecific52xxAccount_AndCancelsCleanly()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test10_ProjectExpense_MapsExpenseTypeToSpecific52xxAccount_AndCancelsCleanly));

            var expense = new Expense
            {
                TenantId = 1, FiscalYearId = 1, ProjectId = 1, ExpenseTitle = "دستمزد اکیپ نصب ریل و کابین",
                Type = ExpenseType.Other, Amount = 18_000_000m, ExpenseDate = new DateTime(2025, 5, 1)
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();

            var voucher = await accounting.CreateVoucherForExpenseAsync(expense);
            var items = await context.JournalVoucherItems.Include(i => i.Account).Where(i => i.JournalVoucherId == voucher.Id).ToListAsync();
            Assert.Contains(items, i => i.Account.Code == "5201" && i.Debit == 18_000_000m);

            await accounting.CancelVoucherByReferenceAsync(AccountingReferenceType.Expense, expense.Id, "حذف آزمایشی هزینه");
            var cancelled = await context.JournalVouchers.FirstAsync(v => v.Id == voucher.Id);
            Assert.Equal(JournalVoucherStatus.Cancelled, cancelled.Status);
        }

        [Fact(DisplayName = "11. چرخه کامل چک دریافتنی: ثبت نزد صندوق (1102) -> واگذاری به بانک (1107) -> وصول نهایی (1101)")]
        public async Task Test11_ReceivableChequeLifecycle_InSafe_ToBankCollection_ToCleared()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test11_ReceivableChequeLifecycle_InSafe_ToBankCollection_ToCleared));

            var bank = new BankAccount { TenantId = 1, Title = "بانک ملت", BankName = "ملت", IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-9001", SayadiId = "1234567890123456", Amount = 80_000_000m,
                IssueDate = new DateTime(2025, 5, 1), DueDate = new DateTime(2025, 6, 1),
                BankName = "بانک صادرات", CustomerId = 1, ProjectId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();

            // گام ۱: ثبت اولیه چک دریافتنی
            var v1 = await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            Assert.Equal(80_000_000m, v1.TotalDebit);

            // گام ۲: واگذاری به بانک جهت وصول
            var v2 = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.InBankForCollection, TargetBankAccountId = bank.Id, ActionDateShamsi = "1404/03/10"
            });
            Assert.Equal(ChequeStatus.InBankForCollection, cheque.Status);

            // گام ۳: وصول نهایی چک و واریز به حساب بانک شرکت
            var v3 = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Cleared, TargetBankAccountId = bank.Id, ActionDateShamsi = "1404/03/12"
            });
            Assert.Equal(ChequeStatus.Cleared, cheque.Status);

            var bankSummary = await accounting.GetBankAccountsBalanceSummaryAsync();
            Assert.Equal(80_000_000m, bankSummary[0].CurrentBalance);
        }

        [Fact(DisplayName = "12. واخواست (برگشت) چک دریافتنی و بدهکار شدن مجدد حساب مشتری (1103)")]
        public async Task Test12_ReceivableCheque_Bounced_RestoresCustomerReceivableDebt()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test12_ReceivableCheque_Bounced_RestoresCustomerReceivableDebt));

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-9002", Amount = 50_000_000m,
                IssueDate = new DateTime(2025, 5, 1), DueDate = new DateTime(2025, 6, 1),
                BankName = "بانک ملی", CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();

            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);

            // برگشت (واخواست) چک
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Bounced, ActionDateShamsi = "1404/03/15", Description = "کسری موجودی صادرکننده"
            });

            var statement = await accounting.GetPartyStatementAsync(AccountingPartyType.Customer, 1);
            Assert.Equal(0m, statement.FinalBookBalance); // ثبت چک -50M و برگشت چک +50M -> بدهی مشتری دوباره باز گشت
            Assert.Equal(50_000_000m, statement.BouncedChequesAmount);
        }

        [Fact(DisplayName = "13. خرج (پشت‌نویسی) چک دریافتنی مشتری به تأمین‌کننده (EndorsedToSupplier)")]
        public async Task Test13_ReceivableCheque_EndorsedToSupplier_TransfersReceivableNoteToSupplierPayable()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test13_ReceivableCheque_EndorsedToSupplier_TransfersReceivableNoteToSupplierPayable));

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-9003", Amount = 60_000_000m,
                IssueDate = new DateTime(2025, 5, 1), DueDate = new DateTime(2025, 6, 1),
                BankName = "بانک پاسارگاد", CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();

            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);

            var vEndorse = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.EndorsedToSupplier, EndorsedToSupplierId = 1, ActionDateShamsi = "1404/02/25"
            });

            Assert.Equal(ChequeStatus.EndorsedToSupplier, cheque.Status);
            Assert.Equal(1, cheque.EndorsedToSupplierId);
            Assert.Equal(60_000_000m, vEndorse.TotalDebit);
        }

        [Fact(DisplayName = "14. چرخه چک پرداختنی شرکت: صدور به تأمین‌کننده (2103) و پاس شدن از حساب بانک (1101)")]
        public async Task Test14_PayableChequeLifecycle_IssuedToClearedFromCompanyBank()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test14_PayableChequeLifecycle_IssuedToClearedFromCompanyBank));

            var bank = new BankAccount { TenantId = 1, Title = "بانک تجارت", BankName = "تجارت", InitialBalance = 200_000_000m, IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();
            await accounting.CreateOpeningVoucherForBankAccountAsync(bank);

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Payable, Status = ChequeStatus.InSafe,
                ChequeNumber = "PCHQ-101", Amount = 70_000_000m,
                IssueDate = new DateTime(2025, 5, 1), DueDate = new DateTime(2025, 6, 15),
                BankName = "بانک تجارت", SupplierId = 1, BankAccountId = bank.Id
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();

            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Cleared, TargetBankAccountId = bank.Id, ActionDateShamsi = "1404/03/25"
            });

            var summary = await accounting.GetBankAccountsBalanceSummaryAsync();
            Assert.Equal(130_000_000m, summary[0].CurrentBalance); // 200M - 70M = 130M
        }

        [Fact(DisplayName = "15. ماشین‌حساب رأس‌گیری چک‌ها: میانگین موزون زمانی/ریالی، تاریخ دقیق رأس و ارزش فعلی (NPV)")]
        public async Task Test15_ChequeRasGiriCalculator_ComputesWeightedAverageDaysDueDateAndNetPresentValue()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test15_ChequeRasGiriCalculator_ComputesWeightedAverageDaysDueDateAndNetPresentValue));

            var baseDate = new DateTime(2025, 4, 1);
            var baseDateShamsi = "1404/01/12";

            var c1 = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "1001", Amount = 100_000_000m, IssueDate = baseDate, DueDate = baseDate.AddDays(30),
                BankName = "بانک ملت", CustomerId = 1
            };
            var c2 = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "1002", Amount = 300_000_000m, IssueDate = baseDate, DueDate = baseDate.AddDays(70),
                BankName = "بانک ملت", CustomerId = 1
            };
            context.Cheques.AddRange(c1, c2);
            await context.SaveChangesAsync();

            // چک ۱: ۱۰۰ میلیون به سررسید ۳۰ روز | چک ۲: ۳۰۰ میلیون به سررسید ۷۰ روز
            // میانگین موزون = (100M * 30 + 300M * 70) / 400M = 24,000,000,000 / 400,000,000 = 60 روز
            var result = await accounting.CalculateChequesRasGiriAsync(new ChequeRasGiriRequestModel
            {
                BaseDateShamsi = baseDateShamsi,
                MonthlyDiscountRatePercent = 3m, // ۳ درصد ماهانه
                ChequeIds = new List<int> { c1.Id, c2.Id }
            });

            Assert.Equal(2, result.ChequeCount);
            Assert.Equal(400_000_000m, result.TotalAmount);
            Assert.Equal(60m, result.WeightedAverageDays);
            Assert.Equal(24_000_000m, result.EstimatedDiscountAmount); // 400M * (3%/30) * 60 = 24M
            Assert.Equal(376_000_000m, result.NetPresentValue);
        }

        [Fact(DisplayName = "16. صورت‌حساب ۳۶۰ درجه طرف‌حساب‌ها: مانده اول دوره، مانده در گردش، چک‌های در جریان و ریسک واقعی")]
        public async Task Test16_CustomerAndSupplierStatement360_ComputesOpeningRunningBalancePendingChequesAndExposure()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test16_CustomerAndSupplierStatement360_ComputesOpeningRunningBalancePendingChequesAndExposure));

            var sale = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SellerUserId = "u1",
                Number = "SI-360", IssueDate = new DateTime(2025, 4, 10), Status = InvoiceStatus.Final,
                Subtotal = 100_000_000m, Total = 100_000_000m, Description = "فاکتور فروش پروژه البرز",
                Items = new List<SalesInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 1, UnitPriceSell = 100_000_000m, LineTotal = 100_000_000m } }
            };
            context.SalesInvoices.Add(sale);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForSalesInvoiceAsync(sale);

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-360", Amount = 40_000_000m,
                IssueDate = new DateTime(2025, 4, 12), DueDate = new DateTime(2025, 5, 20),
                BankName = "بانک ملت", CustomerId = 1, ProjectId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);

            var statement = await accounting.GetPartyStatementAsync(AccountingPartyType.Customer, 1, includeChequeAccounts: false);
            Assert.Equal(60_000_000m, statement.FinalBookBalance); // 100M فاکتور - 40M چک دریافتی
            Assert.Equal(1, statement.PendingChequesCount);
            Assert.Equal(40_000_000m, statement.PendingChequesAmount);
            Assert.Equal(100_000_000m, statement.EffectiveExposureAmount); // 60M دفتری + 40M چک هنوز پاس‌نشده = 100M ریسک واقعی
        }

        [Fact(DisplayName = "17. گزارش تحلیل سنی مطالبات (FIFO Aging): تخصیص پرداخت‌ها از قدیمی‌ترین بدهی‌ها در ۵ بازه زمانی")]
        public async Task Test17_AgingReportFifo_AllocatesPartialPaymentsToOldestInvoicesFirst_Across5Buckets()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test17_AgingReportFifo_AllocatesPartialPaymentsToOldestInvoicesFirst_Across5Buckets));
            await accounting.EnsureDefaultChartOfAccountsAsync(1);

            var accReceivable = await context.AccountingAccounts.FirstAsync(a => a.Code == "1103");
            var accRevenue = await context.AccountingAccounts.FirstAsync(a => a.Code == "4101");
            var accBank = await context.AccountingAccounts.FirstAsync(a => a.Code == "1101");

            var asOfDate = new DateTime(2025, 8, 1);

            // فاکتور ۱: ۱۲۰ روز قبل به مبلغ ۵۰ میلیون | فاکتور ۲: ۴۵ روز قبل به مبلغ ۴۰ میلیون | فاکتور ۳: ۱۰ روز قبل به مبلغ ۳۰ میلیون
            // دریافت علی‌الحساب: ۶۰ میلیون (باید کل فاکتور ۱۲۰ روزه = ۵۰ میلیون و ۱۰ میلیون از فاکتور ۴۵ روزه را تسویه کند)
            context.JournalVouchers.AddRange(
                new JournalVoucher
                {
                    TenantId = 1, FiscalYearId = 1, VoucherNumber = 1, VoucherDate = asOfDate.AddDays(-120),
                    Description = "فاکتور ۱", Status = JournalVoucherStatus.Posted, ReferenceType = AccountingReferenceType.SalesInvoice,
                    TotalDebit = 50_000_000m, TotalCredit = 50_000_000m,
                    Items = new List<JournalVoucherItem>
                    {
                        new() { TenantId = 1, AccountId = accReceivable.Id, CustomerId = 1, Debit = 50_000_000m, Credit = 0m, Description = "فاکتور ۱۲۰ روزه" },
                        new() { TenantId = 1, AccountId = accRevenue.Id, Debit = 0m, Credit = 50_000_000m, Description = "فروش" }
                    }
                },
                new JournalVoucher
                {
                    TenantId = 1, FiscalYearId = 1, VoucherNumber = 2, VoucherDate = asOfDate.AddDays(-45),
                    Description = "فاکتور ۲", Status = JournalVoucherStatus.Posted, ReferenceType = AccountingReferenceType.SalesInvoice,
                    TotalDebit = 40_000_000m, TotalCredit = 40_000_000m,
                    Items = new List<JournalVoucherItem>
                    {
                        new() { TenantId = 1, AccountId = accReceivable.Id, CustomerId = 1, Debit = 40_000_000m, Credit = 0m, Description = "فاکتور ۴۵ روزه" },
                        new() { TenantId = 1, AccountId = accRevenue.Id, Debit = 0m, Credit = 40_000_000m, Description = "فروش" }
                    }
                },
                new JournalVoucher
                {
                    TenantId = 1, FiscalYearId = 1, VoucherNumber = 3, VoucherDate = asOfDate.AddDays(-10),
                    Description = "فاکتور ۳", Status = JournalVoucherStatus.Posted, ReferenceType = AccountingReferenceType.SalesInvoice,
                    TotalDebit = 30_000_000m, TotalCredit = 30_000_000m,
                    Items = new List<JournalVoucherItem>
                    {
                        new() { TenantId = 1, AccountId = accReceivable.Id, CustomerId = 1, Debit = 30_000_000m, Credit = 0m, Description = "فاکتور ۱۰ روزه" },
                        new() { TenantId = 1, AccountId = accRevenue.Id, Debit = 0m, Credit = 30_000_000m, Description = "فروش" }
                    }
                },
                new JournalVoucher
                {
                    TenantId = 1, FiscalYearId = 1, VoucherNumber = 4, VoucherDate = asOfDate.AddDays(-5),
                    Description = "دریافت علی‌الحساب", Status = JournalVoucherStatus.Posted, ReferenceType = AccountingReferenceType.Receipt,
                    TotalDebit = 60_000_000m, TotalCredit = 60_000_000m,
                    Items = new List<JournalVoucherItem>
                    {
                        new() { TenantId = 1, AccountId = accBank.Id, Debit = 60_000_000m, Credit = 0m, Description = "بانک" },
                        new() { TenantId = 1, AccountId = accReceivable.Id, CustomerId = 1, Debit = 0m, Credit = 60_000_000m, Description = "دریافت از مشتری" }
                    }
                }
            );
            await context.SaveChangesAsync();

            var aging = await accounting.GetAgingReportAsync(AccountingPartyType.Customer, asOfDate);
            Assert.Single(aging.Rows);
            var row = aging.Rows[0];
            Assert.Equal(60_000_000m, row.TotalOpenBalance); // (50M + 40M + 30M) - 60M = 60M
            Assert.Equal(0m, row.Bucket91To180);             // فاکتور ۱۲۰ روزه کامل تسویه شد
            Assert.Equal(30_000_000m, row.Bucket31To60);     // از فاکتور ۴۵ روزه ۳۰ میلیون باقی ماند
            Assert.Equal(30_000_000m, row.Bucket0To30);      // فاکتور ۱۰ روزه کامل باز است
        }

        [Fact(DisplayName = "18. عملیات پایان سال مالی: بستن حساب‌های موقت، انتقال تفصیلی‌ها به سال جدید، حفظ گزارش سود و زیان و بازگشایی سال مالی")]
        public async Task Test18_FiscalYearClosingAndReopen_ClosesTempAccounts_TransfersSubLedgersToNewYear_AndReopensCleanly()
        {
            var (context, _, accounting, _, _, currentFy) = CreateTestRig(nameof(Test18_FiscalYearClosingAndReopen_ClosesTempAccounts_TransfersSubLedgersToNewYear_AndReopensCleanly));

            var bank = new BankAccount { TenantId = 1, Title = "بانک ملت", BankName = "ملت", InitialBalance = 100_000_000m, IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();
            await accounting.CreateOpeningVoucherForBankAccountAsync(bank);

            var sale = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1, SellerUserId = "u1",
                Number = "SI-CLOSE", IssueDate = new DateTime(2025, 6, 1), Status = InvoiceStatus.Final,
                Subtotal = 80_000_000m, Total = 80_000_000m, Description = "فروش سال ۱۴۰۴",
                Items = new List<SalesInvoiceItem> { new() { TenantId = 1, ProductId = 1, Quantity = 1, UnitPriceSell = 80_000_000m, LineTotal = 80_000_000m } }
            };
            context.SalesInvoices.Add(sale);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForSalesInvoiceAsync(sale);

            var expense = new Expense
            {
                TenantId = 1, FiscalYearId = 1, ProjectId = 1, ExpenseTitle = "هزینه نصب",
                Type = ExpenseType.Other, Amount = 20_000_000m, ExpenseDate = new DateTime(2025, 6, 10)
            };
            context.Expenses.Add(expense);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForExpenseAsync(expense);

            // ۱. پیش‌نمایش بستن سال مالی ۱۴۰۴ (سود خالص = 80M - 20M = 60M)
            var preview = await accounting.GetFiscalYearClosingPreviewAsync(1);
            Assert.True(preview.CanClose);
            Assert.Equal(80_000_000m, preview.TotalRevenues);
            Assert.Equal(20_000_000m, preview.TotalExpenses);
            Assert.Equal(60_000_000m, preview.NetProfitOrLoss);

            // ۲. اجرای بستن سال مالی ۱۴۰۴ و صدور سند افتتاحیه ۱۴۰۵
            var closeResult = await accounting.CloseFiscalYearAndGenerateOpeningAsync(new CloseFiscalYearRequestModel
            {
                SourceFiscalYearId = 1,
                NextFiscalYearTitle = "سال مالی 1405",
                NextStartDateShamsi = "1405/01/01",
                NextEndDateShamsi = "1405/12/29",
                AutoPostDraftVouchers = true
            });

            Assert.Equal(60_000_000m, closeResult.NetProfitOrLossTransferred);
            Assert.NotNull(closeResult.OpeningVoucherIdInNewYear);

            // ۳. گزارش سود و زیان در سال مالی بسته‌شده ۱۴۰۴ همچنان باید سود واقعی (۶۰ میلیون) را نشان دهد
            var pnlAfterClose = await accounting.GetProfitAndLossAsync();
            Assert.Equal(60_000_000m, pnlAfterClose.NetProfit);

            // ۴. جلوگیری از ثبت سند جدید در سال مالی بسته‌شده
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                accounting.CreateVoucherForExpenseAsync(new Expense
                {
                    Id = 99, TenantId = 1, FiscalYearId = 1, ProjectId = 1, ExpenseTitle = "هزینه غیرمجاز در سال بسته",
                    Type = ExpenseType.Other, Amount = 5_000_000m, ExpenseDate = new DateTime(2025, 7, 1)
                }));

            // ۵. سوئیچ به سال مالی جدید ۱۴۰۵ و بررسی انتقال مانده مشتری (۸۰ میلیون) و بانک (۱۰۰ میلیون)
            currentFy.FiscalYearId = closeResult.NewFiscalYearId;
            var customerStatementInNewYear = await accounting.GetPartyStatementAsync(AccountingPartyType.Customer, 1);
            Assert.Equal(80_000_000m, customerStatementInNewYear.FinalBookBalance);

            // ۶. بازگشایی سال مالی ۱۴۰۴ جهت اصلاحات
            currentFy.FiscalYearId = 1;
            var reopenResult = await accounting.ReopenFiscalYearAsync(1, closeResult.NewFiscalYearId);
            Assert.Equal(2, reopenResult.RemovedClosingVouchersCount);
            Assert.Equal(1, reopenResult.RemovedOpeningVouchersCount);
        }
    }
}
