using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.DataModel.Seeders;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Entities.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
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
            var inventory = new InventoryService(context, uow, currentFy, currentUser, accounting);

            return (context, uow, accounting, inventory, currentUser, currentFy);
        }


        [Fact]
        public async Task SaveChanges_RejectsDirectDeleteOfAnotherTenantsRecord()
        {
            var (context, _, _, _, currentUser, _) = CreateTestRig(nameof(SaveChanges_RejectsDirectDeleteOfAnotherTenantsRecord));
            var foreignCustomer = new Customer
            {
                Id = 999,
                TenantId = 2,
                Name = "Foreign customer",
                CreatedAt = DateTime.UtcNow
            };

            context.Customers.Remove(foreignCustomer);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => context.SaveChangesAsync());
        }

        [Fact(DisplayName = "01. ایجاد خودکار و بدون تکرار ۲۰ سرفصل استاندارد حسابداری (EnsureDefaultChartOfAccounts)")]
        public async Task Test01_EnsureDefaultChartOfAccounts_SeedsAll20StandardAccounts_Idempotently()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(Test01_EnsureDefaultChartOfAccounts_SeedsAll20StandardAccounts_Idempotently));

            // Simulate a tenant that already has part of the system chart; new defaults must still be added.
            context.AccountingAccounts.Add(new AccountingAccount
            {
                TenantId = 1,
                Code = "1101",
                Title = "موجودی نقد و بانک (خزانه)",
                Level = AccountLevel.Subsidiary,
                Category = AccountCategory.Asset,
                Nature = AccountNature.DebitOnly,
                IsSystemAccount = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            await accounting.EnsureDefaultChartOfAccountsAsync(1);
            await accounting.EnsureDefaultChartOfAccountsAsync(1); // فراخوانی مجدد برای تست Idempotency

            var accounts = await context.AccountingAccounts.Where(a => a.TenantId == 1 && !a.IsDeleted).ToListAsync();
            Assert.Equal(18, accounts.Count);
            Assert.Contains(accounts, a => a.Code == "1101" && a.Category == AccountCategory.Asset);
            Assert.Contains(accounts, a => a.Code == "1105" && a.Title.Contains("موجودی کالا"));
            Assert.Contains(accounts, a => a.Code == "2101" && a.Category == AccountCategory.Liability);
            Assert.Contains(accounts, a => a.Code == "3101" && a.Category == AccountCategory.Equity);            
            Assert.Contains(accounts, a => a.Code == "4103" && a.Title.Contains("برگشت از فروش"));
            Assert.Contains(accounts, a => a.Code == "5101" && a.Category == AccountCategory.Expense);
            Assert.Contains(accounts, a => a.Code == "5202" && a.Category == AccountCategory.Expense && a.IsSystemAccount);
            Assert.Contains(accounts, a => a.Code == "4201" && a.Category == AccountCategory.Revenue && a.IsSystemAccount);
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

            var duplicateVoucher = await accounting.CreateVoucherForPurchaseInvoiceAsync(pi2);
            Assert.Equal(voucher2.Id, duplicateVoucher.Id);
            Assert.Equal(1, await context.JournalVouchers.CountAsync(x =>
                x.TenantId == 1 && x.ReferenceType == AccountingReferenceType.PurchaseInvoice
                && x.ReferenceId == pi2.Id && x.Status == JournalVoucherStatus.Posted && !x.IsDeleted));

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

            var fifoLayersAfterSale = await context.InventoryTransactions
                .Where(x => x.TransactionType == InventoryTransactionType.Purchase
                    && x.ProductId == 1 && x.WarehouseId == 1)
                .OrderBy(x => x.Id)
                .ToListAsync();
            Assert.Equal(2m, fifoLayersAfterSale[0].RemainingFifoQuantity);
            Assert.Equal(10m, fifoLayersAfterSale[1].RemainingFifoQuantity);

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
                Subtotal = 3_600_000m, Discount = 300_000m, Total = 3_300_000m,
                Description = "برگشت ۳ عدد با قیمت فاکتور برگشت متفاوت از بهای لایه FIFO",
                Items = new List<PurchaseReturnInvoiceItem>
                {
                    new()
                    {
                        TenantId = 1, ProductId = 1, Quantity = 3,
                        UnitPriceReturn = 1_200_000m, Discount = 300_000m,
                        LineTotal = 3_300_000m
                    }
                }
            };
            context.PurchaseReturnInvoices.Add(purchaseReturn);
            await context.SaveChangesAsync();

            await inventory.ApplyInventoryForPurchaseReturnAsync(purchaseReturn);
            var prVoucher = await accounting.CreateVoucherForPurchaseReturnAsync(purchaseReturn);
            await context.SaveChangesAsync();

            var inv = await context.Inventories.FirstAsync(x => x.ProductId == 1);
            Assert.Equal(7m, inv.Quantity); // 10 - 3 = 7

            // بهای برگشت باید از فاکتور برگشت (۱.۲M فی، ۳۰۰K تخفیف) گرفته شود،
            // نه از بهای ۱M لایه FIFO فاکتور خرید.
            Assert.Equal(957_142.86m, inv.AverageUnitCost); // (10M - 3.3M) / 7
            var returnTransaction = await context.InventoryTransactions.SingleAsync(x =>
                x.ReferenceType == InventoryReferenceType.PurchaseReturnInvoice
                && x.ReferenceId == purchaseReturn.Id);
            Assert.Equal(1_100_000m, returnTransaction.UnitCost);
            Assert.Equal(3_300_000m, returnTransaction.TotalCost);
            var sourceFifoLayer = await context.InventoryTransactions.SingleAsync(x =>
                x.ReferenceType == InventoryReferenceType.PurchaseInvoice && x.ReferenceId == pi.Id);
            Assert.Equal(7m, sourceFifoLayer.RemainingFifoQuantity);

            // سند حسابداری برگشت با تخفیف جداگانه متوازن می‌ماند.
            Assert.Equal(3_600_000m, prVoucher.TotalDebit);
            Assert.Equal(3_600_000m, prVoucher.TotalCredit);

            // ابطال برگشت باید همان بهای فاکتور برگشت را به ارزش موجودی بازگرداند.
            inventory.RollbackInventoryForPurchaseReturn(purchaseReturn);
            await context.SaveChangesAsync();
            Assert.Equal(10m, inv.Quantity);
            Assert.Equal(1_000_000m, inv.AverageUnitCost);
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

        [Fact(DisplayName = "عودت چک دریافتنی سند متوازن صادر می‌کند و وضعیت چک را تغییر می‌دهد")]
        public async Task ReceivableCheque_Returned_RestoresCustomerReceivableDebt()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(ReceivableCheque_Returned_RestoresCustomerReceivableDebt));

            var cheque = new Cheque
            {
                TenantId = 1,
                FiscalYearId = 1,
                Type = ChequeType.Receivable,
                Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-RETURN-01",
                Amount = 25_000_000m,
                IssueDate = new DateTime(2025, 5, 1),
                DueDate = new DateTime(2025, 6, 1),
                BankName = "بانک ملی",
                CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();

            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);

            var voucher = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id,
                NewStatus = ChequeStatus.Returned,
                ActionDateShamsi = "1404/03/16",
                Description = "عودت چک به مشتری"
            });

            Assert.Equal(ChequeStatus.Returned, cheque.Status);
            Assert.Equal(25_000_000m, voucher.TotalDebit);
            Assert.Equal(25_000_000m, voucher.TotalCredit);
            Assert.Equal(AccountingReferenceType.ChequeOperation, voucher.ReferenceType);
            Assert.Equal(cheque.Id, voucher.ReferenceId);
            Assert.Contains(voucher.Items, item => item.Debit == 25_000_000m && item.Credit == 0m);
            Assert.Contains(voucher.Items, item => item.Debit == 0m && item.Credit == 25_000_000m);
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

            // صدور چک پرداختنی باید فقط حساب اسناد پرداختنی (2103) را درگیر کند؛
            // این رویداد نباید به‌عنوان گردش نقدی بانک گزارش شود.
            var afterIssue = Assert.Single(await accounting.GetBankAccountsBalanceSummaryAsync());
            Assert.Equal(200_000_000m, afterIssue.TotalDeposits);
            Assert.Equal(0m, afterIssue.TotalWithdrawals);

            var ledgerAfterIssue = await accounting.GetLedgerAsync(bankAccountId: bank.Id);
            Assert.Single(ledgerAfterIssue);
            Assert.All(ledgerAfterIssue, item => Assert.Equal("1101", item.AccountCode));

            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Cleared, TargetBankAccountId = bank.Id, ActionDateShamsi = "1404/03/25"
            });

            var summary = Assert.Single(await accounting.GetBankAccountsBalanceSummaryAsync());
            Assert.Equal(200_000_000m, summary.TotalDeposits);
            Assert.Equal(70_000_000m, summary.TotalWithdrawals);
            Assert.Equal(130_000_000m, summary.CurrentBalance); // 200M - 70M = 130M

            // صورت‌حساب بانک فقط باید ردیف‌های حساب نقد و بانک (1101) را نشان دهد؛
            // ردیف‌های اسناد پرداختنی (2103) نباید وارد گردش بانک شوند.
            var bankLedger = await accounting.GetLedgerAsync(bankAccountId: bank.Id);
            Assert.Equal(2, bankLedger.Count);
            Assert.All(bankLedger, item => Assert.Equal("1101", item.AccountCode));
            Assert.Equal(200_000_000m, bankLedger.Sum(item => item.Debit));
            Assert.Equal(70_000_000m, bankLedger.Sum(item => item.Credit));
            Assert.Equal(130_000_000m, bankLedger[^1].RunningBalance);
        }

        [Fact(DisplayName = "15. ماشین‌حساب رأس‌گیری چک‌ها: میانگین موزون زمانی/ریالی، تاریخ دقیق رأس و ارزش فعلی (NPV)")]
        public async Task Test15_ChequeRasGiriCalculator_ComputesWeightedAverageDaysDueDateAndNetPresentValue()
        {
            var (context, _, accounting, _, _, currentFiscalYear) = CreateTestRig(nameof(Test15_ChequeRasGiriCalculator_ComputesWeightedAverageDaysDueDateAndNetPresentValue));

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

            context.FiscalYears.Add(new FiscalYear
            {
                Id = 2,
                TenantId = 1,
                Title = "سال مالی 1405",
                StartDate = new DateTime(2026, 3, 21),
                EndDate = new DateTime(2027, 3, 20),
                IsCurrent = false,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Seed the foreign-year cheque while the test principal is scoped to that year.
            var otherFiscalYearCheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 2, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "1003", Amount = 900_000_000m, IssueDate = baseDate, DueDate = baseDate.AddDays(90),
                BankName = "بانک ملت", CustomerId = 1
            };
            currentFiscalYear.FiscalYearId = 2;
            context.Cheques.Add(otherFiscalYearCheque);
            await context.SaveChangesAsync();
            currentFiscalYear.FiscalYearId = 1;

            // چک ۱: ۱۰۰ میلیون به سررسید ۳۰ روز | چک ۲: ۳۰۰ میلیون به سررسید ۷۰ روز
            // میانگین موزون = (100M * 30 + 300M * 70) / 400M = 24,000,000,000 / 400,000,000 = 60 روز
            var result = await accounting.CalculateChequesRasGiriAsync(new ChequeRasGiriRequestModel
            {
                BaseDateShamsi = baseDateShamsi,
                MonthlyDiscountRatePercent = 3m, // ۳ درصد ماهانه
                ChequeIds = new List<int> { c1.Id, c2.Id, otherFiscalYearCheque.Id }
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
        [Fact(DisplayName = "Stocktake uses actual counted quantity and updates FIFO layers")]
        public async Task ApplyAdjustment_UsesActualCountAndReconcilesFifoLayers()
        {
            var (context, _, accounting, inventoryService, _, _) = CreateTestRig(nameof(ApplyAdjustment_UsesActualCountAndReconcilesFifoLayers));

            var inventory = new Inventory
            {
                TenantId = 1,
                WarehouseId = 1,
                ProductId = 1,
                Quantity = 10m,
                AverageUnitCost = 100m,
                CreatedAt = DateTime.UtcNow
            };
            var fifoLayer = new InventoryTransaction
            {
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProductId = 1,
                QuantityChange = 10m,
                UnitCost = 80m,
                TotalCost = 800m,
                RemainingFifoQuantity = 10m,
                TransactionType = InventoryTransactionType.Purchase,
                ReferenceType = InventoryReferenceType.PurchaseInvoice,
                ReferenceId = 77,
                Description = "Initial FIFO layer",
                CreatedAt = DateTime.UtcNow
            };
            context.Inventories.Add(inventory);
            context.InventoryTransactions.Add(fifoLayer);
            await context.SaveChangesAsync();

            await inventoryService.ApplyAdjustmentAsync(new AdjustmentDto
            {
                ProductId = 1,
                WarehouseId = 1,
                ActualQuantity = 7m,
                Description = "Physical count decrease"
            });

            Assert.Equal(7m, inventory.Quantity);
            Assert.Equal(7m, fifoLayer.RemainingFifoQuantity);
            var decrease = await context.InventoryTransactions.SingleAsync(x =>
                x.TransactionType == InventoryTransactionType.Adjustment && x.QuantityChange < 0);
            Assert.Equal(-3m, decrease.QuantityChange);
            Assert.Equal(240m, decrease.TotalCost);
            Assert.Equal(decrease.Id, decrease.ReferenceId);
            var decreaseVoucher = await context.JournalVouchers
                .Include(x => x.Items).ThenInclude(x => x.Account)
                .SingleAsync(x => x.ReferenceType == AccountingReferenceType.InventoryAdjustment
                    && x.ReferenceId == (int)decrease.Id && x.Status == JournalVoucherStatus.Posted);
            Assert.Equal(240m, decreaseVoucher.TotalDebit);
            Assert.Equal(240m, decreaseVoucher.TotalCredit);
            Assert.Contains(decreaseVoucher.Items, x => x.Account.Code == "5202" && x.Debit == 240m && x.Credit == 0m);
            Assert.Contains(decreaseVoucher.Items, x => x.Account.Code == "1105" && x.Credit == 240m && x.Debit == 0m);
            var duplicateDecreaseVoucher = await accounting.CreateVoucherForInventoryAdjustmentAsync(decrease);
            Assert.Equal(decreaseVoucher.Id, duplicateDecreaseVoucher.Id);
            Assert.Equal(1, await context.JournalVouchers.CountAsync(x =>
                x.ReferenceType == AccountingReferenceType.InventoryAdjustment && x.ReferenceId == (int)decrease.Id));

            await inventoryService.ApplyAdjustmentAsync(new AdjustmentDto
            {
                ProductId = 1,
                WarehouseId = 1,
                ActualQuantity = 12m,
                Description = "Physical count increase"
            });

            Assert.Equal(12m, inventory.Quantity);
            var increase = await context.InventoryTransactions.SingleAsync(x =>
                x.TransactionType == InventoryTransactionType.Adjustment && x.QuantityChange > 0);
            Assert.Equal(5m, increase.QuantityChange);
            Assert.Equal(500m, increase.TotalCost);
            Assert.Equal(5m, increase.RemainingFifoQuantity);
            Assert.Equal(increase.Id, increase.ReferenceId);
            var increaseVoucher = await context.JournalVouchers
                .Include(x => x.Items).ThenInclude(x => x.Account)
                .SingleAsync(x => x.ReferenceType == AccountingReferenceType.InventoryAdjustment
                    && x.ReferenceId == (int)increase.Id && x.Status == JournalVoucherStatus.Posted);
            Assert.Equal(500m, increaseVoucher.TotalDebit);
            Assert.Equal(500m, increaseVoucher.TotalCredit);
            Assert.Contains(increaseVoucher.Items, x => x.Account.Code == "1105" && x.Debit == 500m && x.Credit == 0m);
            Assert.Contains(increaseVoucher.Items, x => x.Account.Code == "4201" && x.Credit == 500m && x.Debit == 0m);
            Assert.Equal(2, await context.JournalVouchers.CountAsync(x =>
                x.ReferenceType == AccountingReferenceType.InventoryAdjustment && x.Status == JournalVoucherStatus.Posted));
        }

        [Fact(DisplayName = "Purchase invoice cancellation is blocked after its source FIFO layer has been consumed")]
        public async Task RollbackPurchaseInvoice_RejectsConsumedSourceLayer()
        {
            var (context, _, _, inventoryService, _, _) = CreateTestRig(nameof(RollbackPurchaseInvoice_RejectsConsumedSourceLayer));
            var invoice = new PurchaseInvoice
            {
                Id = 901,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                SupplierId = 1,
                PurchaserUserId = "user-admin",
                Number = "PI-ROLLBACK-901",
                IssueDate = new DateTime(2025, 4, 1),
                Status = InvoiceStatus.Final,
                Description = "Purchase rollback test",
                Items = new List<PurchaseInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 5m, UnitPricePurchase = 100m, LineTotal = 500m }
                }
            };
            var inventory = new Inventory
            {
                TenantId = 1, WarehouseId = 1, ProductId = 1,
                Quantity = 15m, AverageUnitCost = 100m, CreatedAt = DateTime.UtcNow
            };
            var sourceLayer = new InventoryTransaction
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProductId = 1,
                QuantityChange = 5m, RemainingFifoQuantity = 2m, UnitCost = 100m, TotalCost = 500m,
                TransactionType = InventoryTransactionType.Purchase,
                ReferenceType = InventoryReferenceType.PurchaseInvoice,
                ReferenceId = invoice.Id, CreatedAt = DateTime.UtcNow, Description = "Partially consumed source layer"
            };
            context.Inventories.Add(inventory);
            context.InventoryTransactions.AddRange(sourceLayer, new InventoryTransaction
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProductId = 1,
                QuantityChange = 10m, RemainingFifoQuantity = 10m, UnitCost = 100m, TotalCost = 1_000m,
                TransactionType = InventoryTransactionType.Purchase,
                ReferenceType = InventoryReferenceType.PurchaseInvoice,
                ReferenceId = 902, CreatedAt = DateTime.UtcNow, Description = "Other purchase layer"
            });
            await context.SaveChangesAsync();

            Assert.Throws<InvalidOperationException>(() => inventoryService.RollbackInventoryForPurchaseInvoice(invoice));
            Assert.Equal(15m, inventory.Quantity);
            Assert.Equal(2m, sourceLayer.RemainingFifoQuantity);

            sourceLayer.RemainingFifoQuantity = 5m;
            inventoryService.RollbackInventoryForPurchaseInvoice(invoice);
            Assert.Equal(10m, inventory.Quantity);
            Assert.Equal(0m, sourceLayer.RemainingFifoQuantity);
        }

        [Fact(DisplayName = "Sales return cancellation is blocked after its returned FIFO layer has been consumed")]
        public async Task RollbackSalesReturn_RejectsConsumedReturnedLayer()
        {
            var (context, _, _, inventoryService, _, _) = CreateTestRig(nameof(RollbackSalesReturn_RejectsConsumedReturnedLayer));
            var returnInvoice = new SalesReturnInvoice
            {
                Id = 903,
                Number = "SR-ROLLBACK-903",
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                SellerUserId = "user-admin",
                IssueDate = new DateTime(2025, 4, 1),
                Status = InvoiceStatus.Final,
                Items = new List<SalesReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 5m, UnitPriceReturn = 120m, UnitCost = 100m, TotalCost = 500m, LineTotal = 600m }
                }
            };
            var inventory = new Inventory
            {
                TenantId = 1, WarehouseId = 1, ProductId = 1,
                Quantity = 12m, AverageUnitCost = 100m, CreatedAt = DateTime.UtcNow
            };
            var sourceLayer = new InventoryTransaction
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProductId = 1,
                QuantityChange = 5m, RemainingFifoQuantity = 2m, UnitCost = 100m, TotalCost = 500m,
                TransactionType = InventoryTransactionType.ReturnSale,
                ReferenceType = InventoryReferenceType.SalesReturnInvoice,
                ReferenceId = returnInvoice.Id, CreatedAt = DateTime.UtcNow, Description = "Partially consumed returned layer"
            };
            context.Inventories.Add(inventory);
            context.InventoryTransactions.Add(sourceLayer);
            await context.SaveChangesAsync();

            Assert.Throws<InvalidOperationException>(() => inventoryService.RollbackInventoryForSalesReturn(returnInvoice));
            Assert.Equal(12m, inventory.Quantity);
            Assert.Equal(2m, sourceLayer.RemainingFifoQuantity);

            sourceLayer.RemainingFifoQuantity = 5m;
            inventoryService.RollbackInventoryForSalesReturn(returnInvoice);
            Assert.Equal(7m, inventory.Quantity);
            Assert.Equal(0m, sourceLayer.RemainingFifoQuantity);
        }


        private static void SetTenantPrincipal(ControllerBase controller)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("TenantId", "1") }, "UnitTest"))
                }
            };
        }

        [Fact(DisplayName = "Confirm فروش: ردیف‌های تکراری کالا نمی‌توانند سقف برگشت را دور بزنند")]
        public async Task ConfirmSalesReturn_RejectsDuplicateProductRowsThatExceedSourceQuantity()
        {
            var (context, uow, accounting, inventory, _, fiscalYear) =
                CreateTestRig(nameof(ConfirmSalesReturn_RejectsDuplicateProductRowsThatExceedSourceQuantity));

            var source = new SalesInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1,
                SellerUserId = "user-admin", Number = "SI-DUP-1",
                IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000m, Total = 10_000m, Description = "source",
                Items = new List<SalesInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 10m, UnitPriceSell = 1_000m, LineTotal = 10_000m }
                }
            };
            context.SalesInvoices.Add(source);
            await context.SaveChangesAsync();

            var returnInvoice = new SalesReturnInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, ProjectId = 1,
                SalesInvoiceId = source.Id, SellerUserId = "user-admin", Number = "SR-DUP-1",
                IssueDate = new DateTime(2025, 4, 2), Status = InvoiceStatus.Proforma,
                Subtotal = 12_000m, Total = 12_000m, Description = "duplicate rows",
                Items = new List<SalesReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 6m, UnitPriceReturn = 1_000m, LineTotal = 6_000m },
                    new() { TenantId = 1, ProductId = 1, Quantity = 6m, UnitPriceReturn = 1_000m, LineTotal = 6_000m }
                }
            };
            context.SalesReturnInvoices.Add(returnInvoice);
            await context.SaveChangesAsync();

            var controller = new SalesReturnInvoiceController(
                uow, inventory, accounting, fiscalYear, NullLogger<SalesReturnInvoiceController>.Instance);
            SetTenantPrincipal(controller);

            var result = await controller.Confirm(returnInvoice.Id);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(InvoiceStatus.Proforma, returnInvoice.Status);
            Assert.Empty(await context.InventoryTransactions
                .Where(x => x.ReferenceType == InventoryReferenceType.SalesReturnInvoice
                    && x.ReferenceId == returnInvoice.Id)
                .ToListAsync());
            Assert.Empty(await context.JournalVouchers
                .Where(x => x.ReferenceType == AccountingReferenceType.SalesReturnInvoice
                    && x.ReferenceId == returnInvoice.Id)
                .ToListAsync());
        }

        [Fact(DisplayName = "Confirm خرید: ردیف‌های تکراری کالا نمی‌توانند سقف برگشت را دور بزنند")]
        public async Task ConfirmPurchaseReturn_RejectsDuplicateProductRowsThatExceedSourceQuantity()
        {
            var (context, uow, accounting, inventory, _, fiscalYear) =
                CreateTestRig(nameof(ConfirmPurchaseReturn_RejectsDuplicateProductRowsThatExceedSourceQuantity));

            var source = new PurchaseInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1,
                PurchaserUserId = "user-admin", Number = "PI-DUP-1",
                IssueDate = new DateTime(2025, 4, 1), Status = InvoiceStatus.Final,
                Subtotal = 10_000m, Total = 10_000m, Description = "source",
                Items = new List<PurchaseInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 10m, UnitPricePurchase = 1_000m, LineTotal = 10_000m }
                }
            };
            context.PurchaseInvoices.Add(source);
            await context.SaveChangesAsync();

            var returnInvoice = new PurchaseReturnInvoice
            {
                TenantId = 1, FiscalYearId = 1, WarehouseId = 1, SupplierId = 1,
                PurchaseInvoiceId = source.Id, PurchaserUserId = "user-admin", Number = "PR-DUP-1",
                IssueDate = new DateTime(2025, 4, 2), Status = InvoiceStatus.Proforma,
                Subtotal = 12_000m, Total = 12_000m, Description = "duplicate rows",
                Items = new List<PurchaseReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 6m, UnitPriceReturn = 1_000m, LineTotal = 6_000m },
                    new() { TenantId = 1, ProductId = 1, Quantity = 6m, UnitPriceReturn = 1_000m, LineTotal = 6_000m }
                }
            };
            context.PurchaseReturnInvoices.Add(returnInvoice);
            await context.SaveChangesAsync();

            var controller = new PurchaseReturnInvoiceController(
                uow, inventory, accounting, fiscalYear, NullLogger<PurchaseReturnInvoiceController>.Instance);
            SetTenantPrincipal(controller);

            var result = await controller.Confirm(returnInvoice.Id);

            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(InvoiceStatus.Proforma, returnInvoice.Status);
            Assert.Empty(await context.InventoryTransactions
                .Where(x => x.ReferenceType == InventoryReferenceType.PurchaseReturnInvoice
                    && x.ReferenceId == returnInvoice.Id)
                .ToListAsync());
            Assert.Empty(await context.JournalVouchers
                .Where(x => x.ReferenceType == AccountingReferenceType.PurchaseReturnInvoice
                    && x.ReferenceId == returnInvoice.Id)
                .ToListAsync());
        }

    }

    public class FiscalYearMaintenanceSecurityTests
    {
        [Fact]
        public async Task StartupFiscalYearMaintenanceScope_AllowsScopedSeedAndUpdateWithoutAuthenticatedUser()
        {
            var currentUser = new MutableCurrentUserService
            {
                TenantId = null,
                UserId = null,
                IsAuthenticated = false
            };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = null };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("FiscalYearMaintenance_" + Guid.NewGuid().ToString("N"))
                .Options;

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Tenants.Add(new Tenant
            {
                Id = 42,
                TenantCode = "TENANT42",
                TenantTitle = "Tenant 42",
                IsActive = false,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            await FiscalYearSeeder.SeedCurrentFiscalYearAsync(context, 42);

            using (context.BeginTenantFiscalYearMaintenanceScope(42))
            {
                var fiscalYear = await context.FiscalYears.IgnoreQueryFilters()
                    .SingleAsync(f => f.TenantId == 42 && !f.IsDeleted);
                fiscalYear.IsCurrent = false;
                await context.SaveChangesAsync();
            }

            Assert.False(await context.FiscalYears.IgnoreQueryFilters()
                .Where(f => f.TenantId == 42)
                .Select(f => f.IsCurrent)
                .SingleAsync());
        }
    }

    public class TenantProvisioningSecurityTests
    {
        [Fact]
        public async Task AnonymousProvisioningScope_AllowsOnlyInitialFiscalYearForTargetTenant()
        {
            var currentUser = new MutableCurrentUserService
            {
                TenantId = null,
                UserId = null,
                IsAuthenticated = false
            };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = null };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TenantProvisioning_" + Guid.NewGuid().ToString("N"))
                .Options;

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Tenants.Add(new Tenant
            {
                Id = 42,
                TenantCode = "TENANT42",
                TenantTitle = "Tenant 42",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var initialFiscalYear = new FiscalYear
            {
                TenantId = 42,
                Title = "Initial fiscal year",
                StartDate = new DateTime(2026, 3, 21),
                EndDate = new DateTime(2027, 3, 20),
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };

            context.FiscalYears.Add(initialFiscalYear);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => context.SaveChangesAsync());

            using (context.BeginTenantProvisioningScope(42))
            {
                await context.SaveChangesAsync();
            }

            Assert.True(await context.FiscalYears.IgnoreQueryFilters()
                .AnyAsync(f => f.TenantId == 42 && f.Title == "Initial fiscal year"));
        }

        [Fact]
        public async Task AnonymousProvisioningScope_CannotCreateFiscalYearForDifferentTenant()
        {
            var currentUser = new MutableCurrentUserService
            {
                TenantId = null,
                UserId = null,
                IsAuthenticated = false
            };
            var currentFiscalYear = new MutableCurrentFiscalYearService { FiscalYearId = null };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("TenantProvisioningMismatch_" + Guid.NewGuid().ToString("N"))
                .Options;

            await using var context = new ApplicationDbContext(options, currentUser, currentFiscalYear);
            context.Tenants.Add(new Tenant
            {
                Id = 42,
                TenantCode = "TENANT42",
                TenantTitle = "Tenant 42",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            context.FiscalYears.Add(new FiscalYear
            {
                TenantId = 43,
                Title = "Spoofed fiscal year",
                StartDate = new DateTime(2026, 3, 21),
                EndDate = new DateTime(2027, 3, 20),
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });

            using (context.BeginTenantProvisioningScope(42))
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() => context.SaveChangesAsync());
            }
        }

        [Fact(DisplayName = "16. وصول مجدد چک دریافتنی برگشتی: تسویه حساب مشتری (1103)")]
        public async Task ReceivableCheque_BouncedToCleared_CreditsCustomerReceivable()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(ReceivableCheque_BouncedToCleared_CreditsCustomerReceivable));
            var bank = new BankAccount { TenantId = 1, Title = "بانک ملت", BankName = "ملت", IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-REG-16", Amount = 40_000_000m, IssueDate = new DateTime(2025, 5, 1),
                DueDate = new DateTime(2025, 6, 1), BankName = "بانک ملت", CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Bounced, ActionDateShamsi = "1404/03/15"
            });

            var voucher = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Cleared, TargetBankAccountId = bank.Id,
                ActionDateShamsi = "1404/03/20"
            });
            var items = await context.JournalVoucherItems.Include(x => x.Account)
                .Where(x => x.JournalVoucherId == voucher.Id).OrderBy(x => x.RowNumber).ToListAsync();

            Assert.Equal(new[] { "1101", "1103" }, items.Select(x => x.Account.Code).ToArray());
            Assert.Equal(40_000_000m, items[0].Debit);
            Assert.Equal(40_000_000m, items[1].Credit);
        }

        [Fact(DisplayName = "17. واگذاری مجدد چک دریافتنی برگشتی: انتقال از بدهی مشتری به اسناد در جریان وصول")]
        public async Task ReceivableCheque_BouncedToBankCollection_CreditsCustomerReceivable()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(ReceivableCheque_BouncedToBankCollection_CreditsCustomerReceivable));
            var bank = new BankAccount { TenantId = 1, Title = "بانک ملت", BankName = "ملت", IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-REG-17", Amount = 41_000_000m, IssueDate = new DateTime(2025, 5, 1),
                DueDate = new DateTime(2025, 6, 1), BankName = "بانک ملت", CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Bounced, ActionDateShamsi = "1404/03/15"
            });

            var voucher = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.InBankForCollection, TargetBankAccountId = bank.Id,
                ActionDateShamsi = "1404/03/20"
            });
            var items = await context.JournalVoucherItems.Include(x => x.Account)
                .Where(x => x.JournalVoucherId == voucher.Id).OrderBy(x => x.RowNumber).ToListAsync();

            Assert.Equal(new[] { "1107", "1103" }, items.Select(x => x.Account.Code).ToArray());
            Assert.Equal(41_000_000m, items[0].Debit);
            Assert.Equal(41_000_000m, items[1].Credit);
        }

        [Fact(DisplayName = "18. خرج مجدد چک دریافتنی برگشتی: تسویه بدهی تأمین‌کننده و بستن طلب مشتری")]
        public async Task ReceivableCheque_BouncedToEndorsed_CreditsCustomerReceivable()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(ReceivableCheque_BouncedToEndorsed_CreditsCustomerReceivable));
            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Receivable, Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-REG-18", Amount = 42_000_000m, IssueDate = new DateTime(2025, 5, 1),
                DueDate = new DateTime(2025, 6, 1), BankName = "بانک ملی", CustomerId = 1
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Bounced, ActionDateShamsi = "1404/03/15"
            });

            var voucher = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.EndorsedToSupplier, EndorsedToSupplierId = 1,
                ActionDateShamsi = "1404/03/20"
            });
            var items = await context.JournalVoucherItems.Include(x => x.Account)
                .Where(x => x.JournalVoucherId == voucher.Id).OrderBy(x => x.RowNumber).ToListAsync();

            Assert.Equal(new[] { "2101", "1103" }, items.Select(x => x.Account.Code).ToArray());
            Assert.Equal(42_000_000m, items[0].Debit);
            Assert.Equal(42_000_000m, items[1].Credit);
        }

        [Fact(DisplayName = "19. پاس شدن مجدد چک پرداختنی برگشتی: تسویه بدهی تأمین‌کننده (2101)")]
        public async Task PayableCheque_BouncedToCleared_DebitsSupplierPayable()
        {
            var (context, _, accounting, _, _, _) = CreateTestRig(nameof(PayableCheque_BouncedToCleared_DebitsSupplierPayable));
            var bank = new BankAccount { TenantId = 1, Title = "بانک تجارت", BankName = "تجارت", IsDefault = true, IsActive = true };
            context.BankAccounts.Add(bank);
            await context.SaveChangesAsync();

            var cheque = new Cheque
            {
                TenantId = 1, FiscalYearId = 1, Type = ChequeType.Payable, Status = ChequeStatus.InSafe,
                ChequeNumber = "PCHQ-REG-19", Amount = 43_000_000m, IssueDate = new DateTime(2025, 5, 1),
                DueDate = new DateTime(2025, 6, 1), BankName = "بانک تجارت", SupplierId = 1, BankAccountId = bank.Id
            };
            context.Cheques.Add(cheque);
            await context.SaveChangesAsync();
            await accounting.CreateVoucherForChequeRegistrationAsync(cheque);
            await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Bounced, ActionDateShamsi = "1404/03/15"
            });

            var voucher = await accounting.ChangeChequeStatusAndCreateVoucherAsync(new ChequeStatusChangeModel
            {
                ChequeId = cheque.Id, NewStatus = ChequeStatus.Cleared, TargetBankAccountId = bank.Id,
                ActionDateShamsi = "1404/03/20"
            });
            var items = await context.JournalVoucherItems.Include(x => x.Account)
                .Where(x => x.JournalVoucherId == voucher.Id).OrderBy(x => x.RowNumber).ToListAsync();

            Assert.Equal(new[] { "2101", "1101" }, items.Select(x => x.Account.Code).ToArray());
            Assert.Equal(43_000_000m, items[0].Debit);
            Assert.Equal(43_000_000m, items[1].Credit);
        }

    }
}
