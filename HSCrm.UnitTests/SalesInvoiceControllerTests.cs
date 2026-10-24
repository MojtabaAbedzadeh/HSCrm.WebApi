using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Security.Claims;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class SalesInvoiceControllerTests
    {
        private static (
            ApplicationDbContext Context,
            UnitOfWork Uow,
            Mock<IInventoryService> InventoryMock,
            Mock<IAccountingService> AccountingMock,
            Mock<IPaymentSettlementService> SettlementMock,
            MutableCurrentUserService CurrentUser,
            MutableCurrentFiscalYearService CurrentFiscalYear
        ) CreateTestEnvironment(string dbName)
        {
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = "user-100", IsAuthenticated = true };
            var currentFy = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + "_" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options, currentUser, currentFy);

            // Seed required master data
            context.Tenants.Add(new Tenant
            {
                Id = 1,
                TenantCode = "TNT-1",
                TenantTitle = "شرکت تست",
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
                Title = "انبار مرکزی",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.Projects.Add(new Project
            {
                Id = 1,
                TenantId = 1,
                ProjectTitle = "پروژه آسانسور برج سامان",
                CreatedAt = DateTime.UtcNow
            });

            context.Products.Add(new Product
            {
                Id = 1,
                TenantId = 1,
                Code = "PRD-01",
                Name = "موتور آسانسور ساسی",
                CreatedAt = DateTime.UtcNow
            });

            context.SaveChanges();

            var uow = new UnitOfWork(context);
            var inventoryMock = new Mock<IInventoryService>();
            var accountingMock = new Mock<IAccountingService>();
            var settlementMock = new Mock<IPaymentSettlementService>();

            return (context, uow, inventoryMock, accountingMock, settlementMock, currentUser, currentFy);
        }

        private static SalesInvoiceController CreateController(
            UnitOfWork uow,
            Mock<IInventoryService> inventoryMock,
            Mock<IAccountingService> accountingMock,
            Mock<IPaymentSettlementService> settlementMock,
            ICurrentFiscalYearService fiscalYearService,
            int? tenantId = 1,
            string userId = "user-100")
        {
            var controller = new SalesInvoiceController(
                uow,
                inventoryMock.Object,
                settlementMock.Object,
                accountingMock.Object,
                fiscalYearService);

            var claims = new List<Claim>();
            if (tenantId.HasValue)
            {
                claims.Add(new Claim("TenantId", tenantId.Value.ToString()));
            }
            if (!string.IsNullOrEmpty(userId))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };

            return controller;
        }

        [Fact(DisplayName = "1. ایجاد فاکتور فروش: در صورت خالی بودن پایگاه‌داده نباید خطای NullReference بدهد و باید فاکتور با موفقیت ایجاد شود")]
        public async Task CreateSalesInvoice_WhenDatabaseIsEmpty_DoesNotThrowNullReferenceAndCreatesSuccessfully()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesEmptyDb");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            // اطمینان از اینکه جدول فاکتورهای فروش خالی است
            Assert.Empty(env.Context.SalesInvoices);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-0001",
                IssueDate = "1404/01/15",
                SellerUserId = "user-100",
                Status = InvoiceStatus.Proforma,
                InvoiceDiscount = 0,
                InvoiceTax = 0,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new()
                    {
                        ProductId = 1,
                        Quantity = 2,
                        UnitPrice = 50_000_000m,
                        Discount = 0,
                        Tax = 0
                    }
                }
            };

            // Act
            var response = await controller.CreateSalesInvoice(model);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Status);
            Assert.Equal(ApiResultStatusCode.Success, response.StatusCode);

            // بررسی ذخیره شدن در پایگاه داده
            var savedInvoice = await env.Context.SalesInvoices.Include(s => s.Items).FirstOrDefaultAsync(s => s.Number == "SI-0001");
            Assert.NotNull(savedInvoice);
            Assert.Equal(1, savedInvoice.TenantId);
            Assert.Equal(1, savedInvoice.FiscalYearId);
            Assert.Equal(100_000_000m, savedInvoice.Total);
            Assert.Single(savedInvoice.Items);
            Assert.Equal(100_000_000m, savedInvoice.Items.First().LineTotal);
        }

        [Fact(DisplayName = "2. ایجاد فاکتور فروش: در صورت تکراری بودن شماره فاکتور باید خطای Conflict برگرداند")]
        public async Task CreateSalesInvoice_WhenDuplicateInvoiceNumber_ReturnsConflict()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesDuplicate");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            // فاکتور اول را ثبت می‌کنیم
            var existingInvoice = new SalesInvoice
            {
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                Number = "SI-EXISTING",
                IssueDate = DateTime.UtcNow,
                Status = InvoiceStatus.Proforma,
                Subtotal = 10_000_000m,
                Total = 10_000_000m,
                SellerUserId = "user-100",
                Description = "تست",
                CreatedAt = DateTime.UtcNow
            };
            env.Context.SalesInvoices.Add(existingInvoice);
            await env.Context.SaveChangesAsync();

            // فاکتور جدید با همان شماره ارسال می‌کنیم
            var duplicateModel = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-EXISTING",
                IssueDate = "1404/01/15",
                SellerUserId = "user-100",
                Status = InvoiceStatus.Proforma,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 1, UnitPrice = 10_000_000m }
                }
            };

            // Act
            var response = await controller.CreateSalesInvoice(duplicateModel);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.Conflict, response.StatusCode);
        }

        [Fact(DisplayName = "3. ایجاد فاکتور فروش: در صورت تهی یا نال بودن اقلام کالا باید خطای NoContent برگرداند")]
        public async Task CreateSalesInvoice_WhenItemsListEmpty_ReturnsNoContent()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesNoItems");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-NOITEMS",
                IssueDate = "1404/01/15",
                SellerUserId = "user-100",
                Items = new List<SalesInvoiceItemCreateModel>()
            };

            // Act
            var response = await controller.CreateSalesInvoice(model);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.NoContent, response.StatusCode);
        }

        [Fact(DisplayName = "4. ایجاد فاکتور فروش: در صورت خالی بودن شماره فاکتور باید خطای BadRequest برگرداند")]
        public async Task CreateSalesInvoice_WhenInvoiceNumberMissing_ReturnsBadRequest()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesEmptyNumber");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "   ",
                IssueDate = "1404/01/15",
                SellerUserId = "user-100",
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 1, UnitPrice = 10_000_000m }
                }
            };

            // Act
            var response = await controller.CreateSalesInvoice(model);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "5. ایجاد فاکتور فروش: اگر شناسه شرکت (TenantId) وجود نداشته باشد باید BadRequest برگرداند")]
        public async Task CreateSalesInvoice_WhenTenantMissing_ReturnsBadRequest()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesNoTenant");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear, tenantId: null);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 0,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-NOTENANT",
                IssueDate = "1404/01/15",
                SellerUserId = "user-100",
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 1, UnitPrice = 10_000_000m }
                }
            };

            // Act
            var response = await controller.CreateSalesInvoice(model);

            // Assert
            Assert.NotNull(response);
            Assert.False(response.Status);
            Assert.Equal(ApiResultStatusCode.BadRequest, response.StatusCode);
        }

        [Fact(DisplayName = "6. ایجاد فاکتور فروش: فاکتور با وضعیت نهایی (Final) باید انبار و سند حسابداری را فراخوانی کند")]
        public async Task CreateSalesInvoice_WhenStatusFinal_InvokesInventoryAndAccounting()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesFinal");
            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-FINAL-1",
                IssueDate = "1404/01/20",
                SellerUserId = "user-100",
                Status = InvoiceStatus.Final,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 3, UnitPrice = 20_000_000m }
                }
            };

            // Act
            var response = await controller.CreateSalesInvoice(model, InventoryCostingMethod.WeightedAverage);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Status);
            Assert.Equal(ApiResultStatusCode.Success, response.StatusCode);

            // اطمینان از اعمال انبار و صدور سند حسابداری
            env.InventoryMock.Verify(x => x.ApplyInventoryForSalesInvoice(It.IsAny<SalesInvoice>(), InventoryCostingMethod.WeightedAverage), Times.Once);
            env.AccountingMock.Verify(x => x.CreateVoucherForSalesInvoiceAsync(It.IsAny<SalesInvoice>(), InventoryCostingMethod.WeightedAverage), Times.Once);
        }

        [Fact(DisplayName = "7. لیست کشویی انبار (WarehouseDropdownList): در پایگاه‌داده خالی بدون خطا لیست خالی برمی‌گرداند")]
        public void WarehouseDropdownList_WhenNoWarehouses_ReturnsEmptyListSuccessfully()
        {
            // Arrange
            var env = CreateTestEnvironment("WhEmpty");
            // پاک کردن انبار پیش‌فرض
            env.Context.Warehouses.RemoveRange(env.Context.Warehouses);
            env.Context.SaveChanges();

            var whController = new WarehouseController(env.Uow);
            var claims = new List<Claim> { new Claim("TenantId", "1") };
            whController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };

            // Act
            var result = whController.WarehouseDropdownList();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Status);
            var data = result.Data as IEnumerable<DropdownDto>;
            Assert.NotNull(data);
            Assert.Empty(data);
        }

        [Fact(DisplayName = "8. لیست کشویی انبار (WarehouseDropdownList): فقط انبارهای فعال و حذف‌نشده مربوط به همان مستاجر را برمی‌گرداند")]
        public void WarehouseDropdownList_WhenWarehousesExist_ReturnsOnlyActiveAndNonDeleted()
        {
            // Arrange
            var env = CreateTestEnvironment("WhFilter");
            env.Context.Warehouses.RemoveRange(env.Context.Warehouses);

            // انبار فعال مستاجر ۱
            env.Context.Warehouses.Add(new Warehouse { Id = 10, TenantId = 1, Title = "انبار فعال ۱", IsActive = true, IsDeleted = false });
            // انبار غیرفعال مستاجر ۱
            env.Context.Warehouses.Add(new Warehouse { Id = 11, TenantId = 1, Title = "انبار غیرفعال", IsActive = false, IsDeleted = false });
            // انبار حذف شده مستاجر ۱
            env.Context.Warehouses.Add(new Warehouse { Id = 12, TenantId = 1, Title = "انبار حذف شده", IsActive = true, IsDeleted = true });
            // انبار متعلق به مستاجر ۲
            env.Context.Warehouses.Add(new Warehouse { Id = 13, TenantId = 2, Title = "انبار شرکت دیگر", IsActive = true, IsDeleted = false });
            env.Context.SaveChanges();

            var whController = new WarehouseController(env.Uow);
            var claims = new List<Claim> { new Claim("TenantId", "1") };
            whController.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
            };

            // Act
            var result = whController.WarehouseDropdownList();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Status);
            var data = (result.Data as IEnumerable<DropdownDto>)?.ToList();
            Assert.NotNull(data);
            Assert.Single(data);
            Assert.Equal(10, data[0].DrId);
            Assert.Equal("انبار فعال ۱", data[0].DrName);
        }

        [Fact(DisplayName = "9. ایجاد فاکتور فروش در صورت null بودن سال مالی جاری: نباید خطای InvalidOperationException بدهد")]
        public async Task CreateSalesInvoice_WhenCurrentFiscalYearIsNull_DoesNotThrowInvalidOperationExceptionAndCreatesSuccessfully()
        {
            // Arrange
            var env = CreateTestEnvironment("SalesNullFiscal");
            // سال مالی جاری را عمداً نال قرار می‌دهیم
            env.CurrentFiscalYear.FiscalYearId = null;

            var controller = CreateController(env.Uow, env.InventoryMock, env.AccountingMock, env.SettlementMock, env.CurrentFiscalYear);

            var model = new SalesInvoiceCreateModel
            {
                TenantId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                InvoiceNumber = "SI-NULL-FY",
                IssueDate = "1404/01/25",
                SellerUserId = "user-100",
                Status = InvoiceStatus.Proforma,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 1, UnitPrice = 5_000_000m }
                }
            };

            // Act - بررسی عدم پرتاب خطای System.InvalidOperationException: 'Nullable object must have a value.'
            var response = await controller.CreateSalesInvoice(model);

            // Assert
            Assert.NotNull(response);
            Assert.True(response.Status);
            Assert.Equal(ApiResultStatusCode.Success, response.StatusCode);
        }
    }
}
