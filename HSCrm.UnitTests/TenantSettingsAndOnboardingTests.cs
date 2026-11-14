using AutoMapper;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class TenantSettingsAndOnboardingTests
    {
        private static (
            ApplicationDbContext Context,
            UnitOfWork Uow,
            MutableCurrentUserService CurrentUser,
            MutableCurrentFiscalYearService CurrentFiscalYear
        ) CreateEnvironment(string dbName, int tenantId = 1)
        {
            var currentUser = new MutableCurrentUserService { TenantId = tenantId, UserId = "user-admin", IsAuthenticated = true };
            var currentFy = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + "_" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options, currentUser, currentFy);

            context.Tenants.Add(new Tenant
            {
                Id = 1,
                TenantCode = "HS01",
                TenantTitle = "شرکت مهندسی آسانسور تست",
                Currency = "IRR",
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

            context.SaveChanges();

            return (context, new UnitOfWork(context), currentUser, currentFy);
        }

        private static ClaimsPrincipal CreatePrincipal(int tenantId = 1, string userId = "user-admin")
        {
            var claims = new List<Claim>
            {
                new("TenantId", tenantId.ToString()),
                new("FiscalYearId", "1"),
                new(ClaimTypes.NameIdentifier, userId)
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        private static TenantController CreateTenantController(ApplicationDbContext context, UnitOfWork uow, int tenantId = 1)
        {
            var generator = new InvoiceNumberGenerator(context);
            var controller = new TenantController(uow, new HttpContextAccessor(), hosting: null, invoiceNumberGenerator: generator);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(tenantId) }
            };
            return controller;
        }

        [Fact(DisplayName = "1. تنظیمات شرکت: GetCompanySettings مقادیر پیش‌فرض و پیش‌نمایش الگوها را برمی‌گرداند")]
        public void GetCompanySettings_ReturnsTenantDefaultsAndPreviews()
        {
            var env = CreateEnvironment("CompanySettingsGet");
            var controller = CreateTenantController(env.Context, env.Uow);

            var response = controller.GetCompanySettings();

            Assert.True(response.Status);
            Assert.Equal(ApiResultStatusCode.Success, response.StatusCode);
            Assert.NotNull(response.Data);
            Assert.Equal(1, response.Data!.TenantId);
            Assert.Equal("Hybrid", response.Data.InvoiceNumberingMode);
            Assert.Equal("INV-{YEAR}-{SEQ:4}", response.Data.SalesInvoicePattern);
            Assert.False(string.IsNullOrWhiteSpace(response.Data.SalesInvoicePreview));
            Assert.Equal(InventoryCostingMethod.WeightedAverage, response.Data.DefaultCostingMethod);
        }

        [Fact(DisplayName = "2. تنظیمات شرکت: UpdateCompanySettings تنظیمات جدید و تاریخ قفل شمسی را ذخیره می‌کند")]
        public void UpdateCompanySettings_ValidModel_PersistsAllFieldsAndShamsiLockDate()
        {
            var env = CreateEnvironment("CompanySettingsUpdate");
            var controller = CreateTenantController(env.Context, env.Uow);

            var model = new CompanySettingsUpdateModel
            {
                TenantTitle = "شرکت آلفا آسانسور",
                Currency = "IRT",
                TaxId = "10101234567",
                CompanyPhone = "021-88990011",
                CompanyEmail = "info@alpha.ir",
                Address = "تهران، خیابان ولیعصر",
                InvoiceNumberingMode = "Auto",
                SalesInvoicePattern = "{PREFIX}-{YY}-{SEQ:5}",
                PurchaseInvoicePattern = "BUY-{YEAR}-{SEQ:4}",
                SalesReturnPattern = "SR-{YEAR}-{SEQ:4}",
                PurchaseReturnPattern = "PR-{YEAR}-{SEQ:4}",
                DefaultVatPercent = 10m,
                InvoiceFooterText = "گارانتی ۱۲ ماهه شامل قطعات می‌باشد.",
                DefaultCostingMethod = InventoryCostingMethod.FIFO,
                DocumentLockDate = "1404/01/15"
            };

            var response = controller.UpdateCompanySettings(model);

            Assert.True(response.Status);
            Assert.Equal(ApiResultStatusCode.Success, response.StatusCode);
            Assert.NotNull(response.Data);
            Assert.Equal("Auto", response.Data!.InvoiceNumberingMode);
            Assert.Equal(10m, response.Data.DefaultVatPercent);
            Assert.Equal(InventoryCostingMethod.FIFO, response.Data.DefaultCostingMethod);
            Assert.Equal("1404/01/15", response.Data.DocumentLockDateShamsi);
            Assert.NotNull(response.Data.DocumentLockDate);
            Assert.Equal(new DateTime(2025, 4, 4), response.Data.DocumentLockDate!.Value.Date);
        }

        [Theory(DisplayName = "3. تنظیمات شرکت: درصد ارزش افزوده خارج از بازه ۰ تا ۱۰۰ باید رد شود")]
        [InlineData(-1)]
        [InlineData(105)]
        public void UpdateCompanySettings_InvalidVatPercent_ReturnsBadRequest(decimal invalidVat)
        {
            var env = CreateEnvironment("CompanySettingsInvalidVat");
            var controller = CreateTenantController(env.Context, env.Uow);

            var response = controller.UpdateCompanySettings(new CompanySettingsUpdateModel
            {
                TenantTitle = "شرکت تست",
                DefaultVatPercent = invalidVat
            });

            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "4. تنظیمات شرکت: حالت شماره‌گذاری نامعتبر باید BadRequest برگرداند")]
        public void UpdateCompanySettings_InvalidNumberingMode_ReturnsBadRequest()
        {
            var env = CreateEnvironment("CompanySettingsInvalidMode");
            var controller = CreateTenantController(env.Context, env.Uow);

            var response = controller.UpdateCompanySettings(new CompanySettingsUpdateModel
            {
                TenantTitle = "شرکت تست",
                InvoiceNumberingMode = "RandomMode"
            });

            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "5. تنظیمات شرکت: فرمت نامعتبر تاریخ قفل اسناد باید BadRequest برگرداند")]
        public void UpdateCompanySettings_InvalidLockDateFormat_ReturnsBadRequest()
        {
            var env = CreateEnvironment("CompanySettingsInvalidLockDate");
            var controller = CreateTenantController(env.Context, env.Uow);

            var response = controller.UpdateCompanySettings(new CompanySettingsUpdateModel
            {
                TenantTitle = "شرکت تست",
                DocumentLockDate = "not-a-valid-date"
            });

            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "6. راه‌اندازی اولیه: GetOnboardingStatus درصد پیشرفت مراحل شرکت را دقیق محاسبه می‌کند")]
        public void GetOnboardingStatus_TracksCompletionProgress()
        {
            var env = CreateEnvironment("OnboardingProgress");
            var controller = CreateTenantController(env.Context, env.Uow);

            // مرحله اول: پروفایل تلفن و آدرس ندارد، هیچ داده پایه‌ای نیست => 0%
            var initial = controller.GetOnboardingStatus();
            Assert.True(initial.Status);
            Assert.NotNull(initial.Data);
            Assert.Equal(6, initial.Data!.TotalSteps);
            Assert.Equal(0, initial.Data.CompletedSteps);
            Assert.False(initial.Data.IsCompleted);

            // تکمیل پروفایل + افزودن یک حساب بانکی و یک انبار => ۳ از ۶ مرحله (50%)
            var tenant = env.Context.Tenants.First(t => t.Id == 1);
            tenant.CompanyPhone = "021-44556677";
            env.Context.BankAccounts.Add(new BankAccount
            {
                TenantId = 1,
                Title = "حساب جاری بانک ملت",
                BankName = "بانک ملت",
                AccountNumber = "123456",
                InitialBalance = 1000m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            env.Context.Warehouses.Add(new Warehouse
            {
                TenantId = 1,
                Title = "انبار مرکزی",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            env.Context.SaveChanges();

            var partial = controller.GetOnboardingStatus();
            Assert.Equal(3, partial.Data!.CompletedSteps);
            Assert.Equal(50, partial.Data.CompletionPercent);
            Assert.False(partial.Data.IsCompleted);

            // تکمیل بقیه مراحل (کالا، مشتری، تامین‌کننده) => 100%
            env.Context.Products.Add(new Product
            {
                TenantId = 1,
                ProductTitle = "ریل آسانسور",
                ProductUnit = "شاخه",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            env.Context.Customers.Add(new Customer
            {
                TenantId = 1,
                Name = "مشتری نمونه",
                Phone = "09120000001",
                CreatedAt = DateTime.UtcNow
            });
            env.Context.Suppliers.Add(new Supplier
            {
                TenantId = 1,
                SupplierFullName = "تامین‌کننده نمونه",
                SupplierPhone = "09120000002",
                CreatedAt = DateTime.UtcNow
            });
            env.Context.SaveChanges();

            var full = controller.GetOnboardingStatus();
            Assert.Equal(6, full.Data!.CompletedSteps);
            Assert.Equal(100, full.Data.CompletionPercent);
            Assert.True(full.Data.IsCompleted);
        }

        [Fact(DisplayName = "7. فاکتور فروش: در حالت Manual ارسال شماره خالی باید BadRequest برگرداند")]
        public async Task SalesInvoice_ManualModeWithoutNumber_ReturnsBadRequest()
        {
            var env = CreateEnvironment("SalesManualRequired");
            var tenant = env.Context.Tenants.First(t => t.Id == 1);
            tenant.InvoiceNumberingMode = "Manual";
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.Projects.Add(new Project { Id = 1, TenantId = 1, ProjectTitle = "پروژه" });
            env.Context.Products.Add(new Product { Id = 1, TenantId = 1, ProductTitle = "کالا", ProductUnit = "عدد", IsActive = true });
            env.Context.SaveChanges();

            var controller = new SalesInvoiceController(
                env.Uow,
                new Mock<IInventoryService>().Object,
                new Mock<IPaymentSettlementService>().Object,
                new Mock<IAccountingService>().Object,
                env.CurrentFiscalYear)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            var response = await controller.CreateSalesInvoice(new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "   ",
                IssueDate = "1404/01/20",
                SellerUserId = "user-admin",
                Items = new List<SalesInvoiceItemCreateModel> { new() { ProductId = 1, Quantity = 1, UnitPrice = 1000m } }
            });

            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "8. فاکتور فروش: در حالت Auto شماره ارسالی نادیده گرفته شده و از سرویس شماره‌گذاری تولید می‌شود")]
        public async Task SalesInvoice_AutoModeOverridesManualInput()
        {
            var env = CreateEnvironment("SalesAutoOverride");
            var tenant = env.Context.Tenants.First(t => t.Id == 1);
            tenant.InvoiceNumberingMode = "Auto";
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.Projects.Add(new Project { Id = 1, TenantId = 1, ProjectTitle = "پروژه" });
            env.Context.Products.Add(new Product { Id = 1, TenantId = 1, ProductTitle = "کالا", ProductUnit = "عدد", IsActive = true });
            env.Context.SaveChanges();

            var genMock = new Mock<IInvoiceNumberGenerator>();
            genMock.Setup(x => x.GenerateNextSalesInvoiceNumberAsync(1, 1, It.IsAny<DateTime?>()))
                .ReturnsAsync("AUTO-1404-0009");

            var controller = new SalesInvoiceController(
                env.Uow,
                new Mock<IInventoryService>().Object,
                new Mock<IPaymentSettlementService>().Object,
                new Mock<IAccountingService>().Object,
                env.CurrentFiscalYear,
                genMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "USER-MANUAL-999",
                IssueDate = "1404/01/20",
                SellerUserId = "user-admin",
                Status = InvoiceStatus.Proforma,
                Items = new List<SalesInvoiceItemCreateModel> { new() { ProductId = 1, Quantity = 1, UnitPrice = 5000m } }
            };

            var response = await controller.CreateSalesInvoice(model);

            Assert.True(response.Status);
            Assert.Equal("AUTO-1404-0009", model.InvoiceNumber);
            genMock.Verify(x => x.GenerateNextSalesInvoiceNumberAsync(1, 1, It.IsAny<DateTime?>()), Times.Once);
        }

        [Fact(DisplayName = "9. فاکتور فروش: ثبت فاکتور در تاریخ قفل‌شده اسناد باید مسدود شود")]
        public async Task SalesInvoice_OnOrBeforeDocumentLockDate_ReturnsBadRequest()
        {
            var env = CreateEnvironment("SalesLockDate");
            var tenant = env.Context.Tenants.First(t => t.Id == 1);
            // 1404/01/15 == 2025-04-04
            tenant.DocumentLockDate = new DateTime(2025, 4, 4);
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.Projects.Add(new Project { Id = 1, TenantId = 1, ProjectTitle = "پروژه" });
            env.Context.Products.Add(new Product { Id = 1, TenantId = 1, ProductTitle = "کالا", ProductUnit = "عدد", IsActive = true });
            env.Context.SaveChanges();

            var controller = new SalesInvoiceController(
                env.Uow,
                new Mock<IInventoryService>().Object,
                new Mock<IPaymentSettlementService>().Object,
                new Mock<IAccountingService>().Object,
                env.CurrentFiscalYear)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            var response = await controller.CreateSalesInvoice(new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-LOCKED",
                IssueDate = "1404/01/10",
                SellerUserId = "user-admin",
                Items = new List<SalesInvoiceItemCreateModel> { new() { ProductId = 1, Quantity = 1, UnitPrice = 1000m } }
            });

            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "10. فاکتور خرید: در حالت Manual شماره خالی رد شده و تاریخ قفل‌شده مسدود می‌شود (بدون مشتری/پروژه)")]
        public async Task PurchaseInvoice_ManualModeAndLockDate_AreEnforced()
        {
            var env = CreateEnvironment("PurchaseRules");
            var tenant = env.Context.Tenants.First(t => t.Id == 1);
            tenant.InvoiceNumberingMode = "Manual";
            tenant.DocumentLockDate = new DateTime(2025, 4, 4); // 1404/01/15
            env.Context.Suppliers.Add(new Supplier { Id = 1, TenantId = 1, SupplierFullName = "تامین‌کننده تست", SupplierPhone = "09120000000" });
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.Products.Add(new Product { Id = 1, TenantId = 1, ProductTitle = "کالا", ProductUnit = "عدد", IsActive = true });
            env.Context.SaveChanges();

            var controller = new PurchaseInvoiceController(
                env.Uow,
                new Mock<IInventoryService>().Object,
                env.CurrentFiscalYear,
                new Mock<IPaymentSettlementService>().Object,
                new Mock<ILogger<PurchaseInvoiceController>>().Object,
                new Mock<IAccountingService>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            // 1) تاریخ قفل‌شده
            var lockedRes = await controller.Create(new PurchaseInvoiceCreateModel
            {
                TenantId = 1,
                SupplierId = 1,
                WarehouseId = 1,
                PurchaserId = "user-admin",
                InvoiceNumber = "PI-1",
                IssueDate = "1404/01/10",
                Items = new List<PurchaseInvoiceItemCreateModel> { new() { ProductId = 1, Quantity = 1, UnitPrice = 1000m } }
            });
            Assert.False(lockedRes.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, lockedRes.StatusCode);

            // 2) تاریخ مجاز اما شماره فاکتور خالی در حالت Manual
            var manualRes = await controller.Create(new PurchaseInvoiceCreateModel
            {
                TenantId = 1,
                SupplierId = 1,
                WarehouseId = 1,
                PurchaserId = "user-admin",
                InvoiceNumber = "",
                IssueDate = "1404/01/20",
                Items = new List<PurchaseInvoiceItemCreateModel> { new() { ProductId = 1, Quantity = 1, UnitPrice = 1000m } }
            });
            Assert.False(manualRes.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, manualRes.StatusCode);
        }

        [Fact(DisplayName = "11. حذف کاربر: حذف کاربر متعلق به همان مستأجر باید رکورد را از دیتابیس حذف کند و کاربر شرکت دیگر محفوظ بماند")]
        public async Task UserController_Delete_RemovesSameTenantUserAndIsolatesCrossTenant()
        {
            var env = CreateEnvironment("UserDeleteIsolation", tenantId: 1);

            var userTenant1 = new ApplicationUser
            {
                Id = "usr-t1",
                TenantId = 1,
                UserName = "employee1",
                UserFirstName = "علی",
                UserLastName = "رضایی",
                PhoneNumber = "09121110001",
                UserStatus = true
            };
            env.Context.Users.Add(userTenant1);
            await env.Context.SaveChangesAsync();

            env.CurrentUser.TenantId = 2;
            var userTenant2 = new ApplicationUser
            {
                Id = "usr-t2",
                TenantId = 2,
                UserName = "employee2",
                UserFirstName = "سارا",
                UserLastName = "محمدی",
                PhoneNumber = "09121110002",
                UserStatus = true
            };
            env.Context.Users.Add(userTenant2);
            await env.Context.SaveChangesAsync();
            env.CurrentUser.TenantId = 1;

            var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            userManagerMock.Setup(m => m.FindByIdAsync("usr-t1")).ReturnsAsync(userTenant1);
            userManagerMock.Setup(m => m.FindByIdAsync("usr-t2")).ReturnsAsync(userTenant2);

            var controller = new UserController(
                env.Uow,
                userManagerMock.Object,
                new Mock<IMapper>().Object,
                env.Context,
                new Mock<IRoleService>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal(tenantId: 1) } }
            };

            // تلاش برای حذف کاربر شرکت ۲ باید با NotFound رد شود
            var crossTenantRes = await controller.Delete("usr-t2");
            Assert.False(crossTenantRes.Status);
            Assert.Equal(ApiResultStatusCode.NotFound, crossTenantRes.StatusCode);
            Assert.NotNull(await env.Context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == "usr-t2"));

            // حذف کاربر شرکت ۱ باید انجام و در دیتابیس ذخیره شود
            var deleteRes = await controller.Delete("usr-t1");
            Assert.True(deleteRes.Status);
            Assert.Equal(ApiResultStatusCode.Success, deleteRes.StatusCode);
            Assert.Null(await env.Context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == "usr-t1"));
        }

        [Fact(DisplayName = "8. Swagger: پارامترهای IFormFile نباید دارای صفت [FromForm] باشند")]
        public void Controllers_IFormFileParameters_ShouldNotUseFromFormAttribute()
        {
            var controllerTypes = typeof(TenantController).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

            var invalidParams = controllerTypes
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .SelectMany(m => m.GetParameters().Select(p => new { Method = m, Param = p }))
                .Where(x => typeof(IFormFile).IsAssignableFrom(x.Param.ParameterType)
                            && x.Param.GetCustomAttribute<FromFormAttribute>() != null)
                .Select(x => $"{x.Method.DeclaringType?.Name}.{x.Method.Name}({x.Param.Name})")
                .ToList();

            Assert.Empty(invalidParams);
        }

        [Fact(DisplayName = "9. حسابداری: سند افتتاحیه بانک باید قبل از پرداخت همان روز مرتب شود تا مانده در گردش درست محاسبه گردد")]
        public async Task GetLedgerAsync_OpeningBalanceBeforeSameDayPayment_ComputesRunningBalanceCorrectly()
        {
            var env = CreateEnvironment(nameof(GetLedgerAsync_OpeningBalanceBeforeSameDayPayment_ComputesRunningBalanceCorrectly));

            var bank = new BankAccount
            {
                Id = 10,
                TenantId = 1,
                Title = "جاری ملت",
                BankName = "بانک ملی",
                InitialBalance = 500_000_000m,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            env.Context.BankAccounts.Add(bank);

            var accCashBank = new AccountingAccount
            {
                Id = 100,
                TenantId = 1,
                Code = AccountingService.ACC_CASH_BANK,
                Title = "موجودی نقد و بانک (خزانه)",
                Level = AccountLevel.Subsidiary,
                Category = AccountCategory.Asset,
                Nature = AccountNature.DebitOnly,
                IsSystemAccount = true,
                CreatedAt = DateTime.UtcNow
            };
            env.Context.AccountingAccounts.Add(accCashBank);

            var todayMidnight = DateTime.UtcNow.Date;
            // سند #3 افتتاحیه با ساعت ۱۱ (شبیه‌سازی سند ثبت‌شده در میانه روز)
            var vOpening = new JournalVoucher
            {
                Id = 3,
                TenantId = 1,
                FiscalYearId = 1,
                VoucherNumber = 3,
                VoucherDate = todayMidnight.AddHours(11),
                Description = "موجودی اول دوره جاری ملت",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.OpeningBalance,
                TotalDebit = 500_000_000m,
                TotalCredit = 500_000_000m,
                CreatedAt = DateTime.UtcNow,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = 1,
                        RowNumber = 1,
                        AccountId = 100,
                        BankAccountId = 10,
                        Description = "موجودی اول دوره جاری ملت (بانک ملی)",
                        Debit = 500_000_000m,
                        Credit = 0m,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

            // سند #6 پرداخت همان روز با ساعت 00:00 (تاریخ انتخاب‌شده از تقویم)
            var vPayment = new JournalVoucher
            {
                Id = 6,
                TenantId = 1,
                FiscalYearId = 1,
                VoucherNumber = 6,
                VoucherDate = todayMidnight,
                Description = "کسر از جاری ملت بابت پرداخت شماره 1003",
                Status = JournalVoucherStatus.Posted,
                ReferenceType = AccountingReferenceType.Payment,
                TotalDebit = 45_420_900m,
                TotalCredit = 45_420_900m,
                CreatedAt = DateTime.UtcNow,
                Items = new List<JournalVoucherItem>
                {
                    new()
                    {
                        TenantId = 1,
                        RowNumber = 1,
                        AccountId = 100,
                        BankAccountId = 10,
                        Description = "کسر از جاری ملت بابت پرداخت شماره 1003",
                        Debit = 0m,
                        Credit = 45_420_900m,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

            env.Context.JournalVouchers.AddRange(vOpening, vPayment);
            await env.Context.SaveChangesAsync();

            var svc = new AccountingService(env.Context, env.Uow, env.CurrentUser, env.CurrentFiscalYear);
            var ledger = await svc.GetLedgerAsync(bankAccountId: 10);

            Assert.Equal(2, ledger.Count);
            Assert.Equal(3, ledger[0].VoucherNumber);
            Assert.Equal(500_000_000m, ledger[0].RunningBalance);
            Assert.Equal(6, ledger[1].VoucherNumber);
            Assert.Equal(454_579_100m, ledger[1].RunningBalance);
        }

        [Fact(DisplayName = "10. سریال‌سازی JSON: چرخه ناوبری فاکتور فروش با IgnoreCycles نباید خطا دهد")]
        public void SalesInvoiceSerialization_WithIgnoreCycles_DoesNotThrowOnNavigationLoop()
        {
            var invoice = new SalesInvoice
            {
                Id = 1,
                TenantId = 1,
                FiscalYearId = 1,
                Number = "SINV-1",
                SellerUserId = "u1",
                Description = "تست",
                IssueDate = DateTime.UtcNow,
                Status = InvoiceStatus.Final,
                Total = 1000m
            };
            var item = new SalesInvoiceItem
            {
                Id = 1,
                TenantId = 1,
                SalesInvoiceId = 1,
                SalesInvoice = invoice,
                ProductId = 5,
                Quantity = 1,
                UnitPriceSell = 1000m,
                LineTotal = 1000m
            };
            invoice.Items.Add(item);

            var payload = new ApiResponse<SalesInvoice>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "موفق",
                Data = invoice
            };

            var opts = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            var json = JsonSerializer.Serialize(payload, opts);
            Assert.Contains("SINV-1", json);
        }

        [Fact(DisplayName = "11. دریافت وجه: مدل بدون UserId/Tenant پذیرفته شده و CustomerId از فاکتور فروش بازیابی می‌شود")]
        public async Task ReceiptController_CreateWithoutUserIdOrTenant_SucceedsAndResolvesCustomer()
        {
            var env = CreateEnvironment("ReceiptCreateAutoCustomer");
            env.Context.Customers.Add(new Customer { Id = 7, TenantId = 1, Name = "مشتری تستی" });
            env.Context.Projects.Add(new Project { Id = 8, TenantId = 1, ProjectTitle = "پروژه تستی", CustomerId = 7 });
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.SalesInvoices.Add(new SalesInvoice
            {
                Id = 9,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 8,
                Number = "SINV-9",
                SellerUserId = "user-admin",
                Description = "تست",
                IssueDate = DateTime.UtcNow,
                Status = InvoiceStatus.Final,
                Total = 10_000_000m
            });
            env.Context.SaveChanges();

            var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile(new Automation.WebApi.AutoMapperProfile.AutoMapping()));
            var mapper = mapperConfig.CreateMapper();
            var accMock = new Mock<IAccountingService>();
            accMock.Setup(x => x.CreateVoucherForReceiptAsync(It.IsAny<Receipt>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new JournalVoucher { VoucherNumber = 1, Description = "سند" });

            var controller = new ReceiptController(
                env.Uow,
                mapper,
                env.CurrentFiscalYear,
                env.CurrentUser,
                accMock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            var actionRes = await controller.Create(new ReceiptCreateModel
            {
                SalesInvoiceId = 9,
                Amount = 4_000_000m,
                ReceiptDate = DateTime.UtcNow.Date,
                Method = "BankTransfer"
            }, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(actionRes.Result);
            var payload = Assert.IsType<ApiResponse<ReceiptModel>>(ok.Value);
            Assert.True(payload.Status);
            Assert.NotNull(payload.Data);
            Assert.Equal(7, payload.Data!.CustomerId);
            var saved = await env.Context.Receipts.FirstAsync();
            Assert.Equal(7, saved.CustomerId);
            Assert.Equal("user-admin", saved.CreatedById);
        }

        [Fact(DisplayName = "12. تسویه فاکتور فروش: دریافت نقدی + چک دریافتنی معتبر در PaidAmount و RemainingAmount لحاظ می‌شوند")]
        public async Task PaymentSettlement_SalesInvoice_IncludesReceiptsAndActiveReceivableCheques()
        {
            var env = CreateEnvironment("SalesSettlementWithCheque");
            env.Context.Customers.Add(new Customer { Id = 1, TenantId = 1, Name = "مشتری" });
            env.Context.Projects.Add(new Project { Id = 1, TenantId = 1, ProjectTitle = "پروژه", CustomerId = 1 });
            env.Context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار", IsActive = true });
            env.Context.SalesInvoices.Add(new SalesInvoice
            {
                Id = 10,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                Number = "SINV-10",
                SellerUserId = "user-admin",
                Description = "تست",
                IssueDate = DateTime.UtcNow,
                Status = InvoiceStatus.Final,
                Total = 50_000_000m
            });
            env.Context.SaveChanges();

            var accMock = new Mock<IAccountingService>();
            accMock.Setup(x => x.CreateVoucherForChequeRegistrationAsync(It.IsAny<Cheque>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new JournalVoucher { VoucherNumber = 2, Description = "سند چک" });

            var chequeController = new ChequeController(env.Uow, accMock.Object, env.CurrentFiscalYear)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = CreatePrincipal() } }
            };

            var res = await chequeController.Create(new ChequeCreateModel
            {
                Type = ChequeType.Receivable,
                ChequeNumber = "CHQ-101",
                Amount = 20_000_000m,
                IssueDateShamsi = "1404/02/10",
                DueDateShamsi = "1404/04/10",
                BankName = "ملت",
                SalesInvoiceId = 10
            }, CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(res.Result);
            var apiRes = Assert.IsType<ApiResponse<Cheque>>(ok.Value);
            Assert.True(apiRes.Status);
            Assert.Equal(1, apiRes.Data!.CustomerId);
            Assert.Equal(10, apiRes.Data.SalesInvoiceId);

            env.Context.Receipts.Add(new Receipt
            {
                TenantId = 1,
                FiscalYearId = 1,
                SalesInvoiceId = 10,
                CustomerId = 1,
                Amount = 10_000_000m,
                ReceiptDate = DateTime.UtcNow,
                Method = "Cash",
                CreatedAt = DateTime.UtcNow
            });
            await env.Context.SaveChangesAsync();

            var settlementSvc = new PaymentSettlementService(env.Context);
            var summary = settlementSvc.BuildSalesInvoiceSummary(10, 50_000_000m);

            Assert.Equal(30_000_000m, summary.PaidAmount);
            Assert.Equal(20_000_000m, summary.RemainingAmount);
            Assert.Equal(SettlementStatus.Partial, summary.SettlementStatus);
        }

        [Fact(DisplayName = "13. تسویه فاکتور خرید: چک پرداختنی معتبر لحاظ می‌شود و چک برگشتی/باطل‌شده کسر می‌گردد")]
        public void PaymentSettlement_PurchaseInvoice_IncludesActiveChequesAndExcludesBounced()
        {
            var env = CreateEnvironment("PurchaseSettlementWithCheque");
            env.Context.Payments.Add(new Payment
            {
                TenantId = 1,
                FiscalYearId = 1,
                PurchaseInvoiceId = 20,
                Amount = 15_000_000m,
                PaymentDate = DateTime.UtcNow,
                Method = "Transfer",
                CreatedAt = DateTime.UtcNow
            });
            env.Context.Cheques.AddRange(
                new Cheque
                {
                    TenantId = 1,
                    FiscalYearId = 1,
                    Type = ChequeType.Payable,
                    Status = ChequeStatus.InSafe,
                    ChequeNumber = "P-1",
                    BankName = "تجارت",
                    PurchaseInvoiceId = 20,
                    Amount = 25_000_000m,
                    IssueDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow
                },
                new Cheque
                {
                    TenantId = 1,
                    FiscalYearId = 1,
                    Type = ChequeType.Payable,
                    Status = ChequeStatus.Bounced,
                    ChequeNumber = "P-2",
                    BankName = "تجارت",
                    PurchaseInvoiceId = 20,
                    Amount = 10_000_000m,
                    IssueDate = DateTime.UtcNow,
                    DueDate = DateTime.UtcNow.AddDays(45),
                    CreatedAt = DateTime.UtcNow
                });
            env.Context.SaveChanges();

            var settlementSvc = new PaymentSettlementService(env.Context);
            var summary = settlementSvc.BuildPurchaseInvoiceSummary(20, 50_000_000m);

            Assert.Equal(40_000_000m, summary.PaidAmount);
            Assert.Equal(10_000_000m, summary.RemainingAmount);
            Assert.Equal(SettlementStatus.Partial, summary.SettlementStatus);
        }
    }
}
