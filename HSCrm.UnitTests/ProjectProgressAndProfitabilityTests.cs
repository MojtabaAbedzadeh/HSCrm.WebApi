using System.Security.Claims;
using System.Text;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class ProjectProgressAndProfitabilityTests
    {
        private static (
            ApplicationDbContext Context,
            MutableCurrentUserService CurrentUser,
            MutableCurrentFiscalYearService CurrentFy
        ) CreateEnv(string dbName, string userId = "mgr-1")
        {
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = userId, IsAuthenticated = true };
            var currentFy = new MutableCurrentFiscalYearService { FiscalYearId = 1 };

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName + "_" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new ApplicationDbContext(options, currentUser, currentFy);

            context.Tenants.Add(new Tenant
            {
                Id = 1,
                TenantCode = "T01",
                TenantTitle = "شرکت آسانسور فاز سه",
                Currency = "IRR",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.FiscalYears.Add(new FiscalYear
            {
                Id = 1,
                TenantId = 1,
                Title = "سال مالی 1405",
                StartDate = new DateTime(2026, 3, 21),
                EndDate = new DateTime(2027, 3, 20),
                IsCurrent = true,
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            });

            var roleOwner = new ApplicationRole { Id = "r-owner", Name = "Owner", NormalizedName = "OWNER", RoleFaName = "مدیر", TenantId = 1 };
            var roleUser = new ApplicationRole { Id = "r-user", Name = "User", NormalizedName = "USER", RoleFaName = "کاربر", TenantId = 1 };
            context.Roles.AddRange(roleOwner, roleUser);

            context.Users.AddRange(
                new ApplicationUser
                {
                    Id = "mgr-1",
                    UserFirstName = "مدیر",
                    UserLastName = "پروژه",
                    UserName = "manager",
                    NormalizedUserName = "MANAGER",
                    TenantId = 1,
                    IsTenantOwner = true,
                    UserStatus = true,
                    UserType = 4
                },
                new ApplicationUser
                {
                    Id = "emp-1",
                    UserFirstName = "مهندس",
                    UserLastName = "ناظر",
                    UserName = "emp1",
                    NormalizedUserName = "EMP1",
                    TenantId = 1,
                    IsTenantOwner = false,
                    UserStatus = true,
                    UserType = 0
                },
                new ApplicationUser
                {
                    Id = "emp-2",
                    UserFirstName = "کارمند",
                    UserLastName = "غیرمرتبط",
                    UserName = "emp2",
                    NormalizedUserName = "EMP2",
                    TenantId = 1,
                    IsTenantOwner = false,
                    UserStatus = true,
                    UserType = 0
                });

            context.UserRoles.AddRange(
                new UserRole { UserId = "mgr-1", RoleId = "r-owner" },
                new UserRole { UserId = "emp-1", RoleId = "r-user" },
                new UserRole { UserId = "emp-2", RoleId = "r-user" });

            var customer = new Customer { Id = 1, TenantId = 1, Name = "کارفرمای نمونه", CreatedAt = DateTime.UtcNow };
            context.Customers.Add(customer);

            context.Projects.AddRange(
                new Project
                {
                    Id = 1,
                    TenantId = 1,
                    CustomerId = 1,
                    ProjectTitle = "نصب آسانسور برج سپهر",
                    ProjectLocation = "تهران - ونک",
                    EstimatedBudget = 120_000_000m,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                },
                new Project
                {
                    Id = 2,
                    TenantId = 1,
                    CustomerId = 1,
                    ProjectTitle = "پروژه محرمانه ۲",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                });

            context.ProjectMembers.Add(new ProjectMember
            {
                Id = 1,
                TenantId = 1,
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "سرپرست کارگاه",
                HourlyRate = 500_000m,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });

            context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار مرکزی", IsActive = true, CreatedAt = DateTime.UtcNow });
            context.Products.AddRange(
                new Product { Id = 1, TenantId = 1, ProductTitle = "موتور گیرلس", ProductUnit = "دستگاه", CreatedAt = DateTime.UtcNow },
                new Product { Id = 2, TenantId = 1, ProductTitle = "کابل بکسل", ProductUnit = "متر", CreatedAt = DateTime.UtcNow });

            context.SaveChanges();
            return (context, currentUser, currentFy);
        }

        [Fact(DisplayName = "1. مایلستون‌ها: محاسبه پیشرفت وزنی پروژه و تغییر خودکار وضعیت")]
        public async Task Milestones_WeightedProgressCalculation_UpdatesProjectProgress()
        {
            var env = CreateEnv("MilestonesWeighted");
            var access = new ProjectAccessService(env.Context);
            var svc = new ProjectProgressService(env.Context, access);

            var m1 = await svc.CreateMilestoneAsync(1, "mgr-1", new ProjectMilestoneCreateModel
            {
                ProjectId = 1,
                Title = "آهن‌کشی و ریل‌گذاری",
                SortOrder = 1,
                WeightPercent = 60m,
                ProgressPercent = 100m,
                DueDate = "2026-06-01"
            });

            var m2 = await svc.CreateMilestoneAsync(1, "mgr-1", new ProjectMilestoneCreateModel
            {
                ProjectId = 1,
                Title = "نصب موتور و تابلو فرمان",
                SortOrder = 2,
                WeightPercent = 40m,
                ProgressPercent = 50m,
                DueDate = "2026-07-01"
            });

            Assert.Equal(MilestoneStatus.Completed, m1.Status);
            Assert.NotNull(m1.CompletedAt);
            Assert.Equal(MilestoneStatus.InProgress, m2.Status);

            var project = await env.Context.Projects.FirstAsync(p => p.Id == 1);
            Assert.Equal(80m, project.ProgressPercent);

            await svc.DeleteMilestoneAsync(1, "mgr-1", m2.Id);
            project = await env.Context.Projects.FirstAsync(p => p.Id == 1);
            Assert.Equal(100m, project.ProgressPercent);
        }

        [Fact(DisplayName = "2. گزارش پیشرفت کارگاهی: ثبت تایم‌لاین و پیوست فایل/تصویر در Attachment و گالری")]
        public async Task AddProgressLog_WithAttachments_StoresFilesAndUpdatesTimelineGallery()
        {
            var env = CreateEnv("ProgressLogAttachments", "emp-1");
            var access = new ProjectAccessService(env.Context);
            var svc = new ProjectProgressService(env.Context, access);

            var tempRoot = Path.Combine(Path.GetTempPath(), "hscrm_p3_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                var ms = await svc.CreateMilestoneAsync(1, "emp-1", new ProjectMilestoneCreateModel
                {
                    ProjectId = 1,
                    Title = "تست و راه‌اندازی",
                    SortOrder = 1,
                    WeightPercent = 100m,
                    ProgressPercent = 20m
                });

                var log = await svc.AddProgressLogAsync(1, 1, "emp-1", new ProjectProgressLogCreateInput
                {
                    ProjectId = 1,
                    MilestoneId = ms.Id,
                    LogDate = "2026-05-10",
                    Title = "پایان نصب ریل‌ها در چاه آسانسور",
                    ReportText = "تمامی ریل‌های راهنما تراز و محکم شدند.",
                    ProgressPercent = 75m,
                    WorkshopArea = "چاهک طبقه اول تا دهم",
                    Files = new List<ProjectAttachmentUpload>
                    {
                        new()
                        {
                            FileName = "rail-install.jpg",
                            ContentType = "image/jpeg",
                            Length = 12,
                            Content = Encoding.UTF8.GetBytes("FAKEJPEGDATA")
                        },
                        new()
                        {
                            FileName = "inspection-report.pdf",
                            ContentType = "application/pdf",
                            Length = 10,
                            Content = Encoding.UTF8.GetBytes("%PDF-1.4!!")
                        }
                    }
                }, tempRoot);

                Assert.Equal(2, log.Attachments.Count);
                Assert.Contains(log.Attachments, a => a.IsImage && a.FileName == "rail-install.jpg");
                Assert.Contains(log.Attachments, a => !a.IsImage && a.FileName == "inspection-report.pdf");

                var savedAtt = await env.Context.Attachments.FirstAsync(a => a.EntityName == "ProjectProgressLog" && a.EntityId == log.Id && a.FileName == "rail-install.jpg");
                var expectedPhysical = Path.Combine(tempRoot, savedAtt.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(expectedPhysical));

                var project = await env.Context.Projects.FirstAsync(p => p.Id == 1);
                Assert.Equal(75m, project.ProgressPercent);

                var timeline = await svc.GetTimelineAsync(1, "emp-1", 1);
                Assert.Single(timeline);
                Assert.Equal(2, timeline[0].Attachments.Count);

                var gallery = await svc.GetGalleryAsync(1, "emp-1", 1);
                Assert.Equal(2, gallery.Count);

                await svc.DeleteProgressLogAsync(1, "emp-1", log.Id);
                var galleryAfterDelete = await svc.GetGalleryAsync(1, "emp-1", 1);
                Assert.Empty(galleryAfterDelete);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }

        [Fact(DisplayName = "3. امنیت پروژه‌محور: کاربر غیرعضو نباید به مستندات و سود و زیان پروژه دسترسی داشته باشد")]
        public async Task UnassignedEmployee_CannotAccessProgressOrProfitability()
        {
            var env = CreateEnv("ProgressAccessControl", "emp-2");
            var access = new ProjectAccessService(env.Context);
            var svc = new ProjectProgressService(env.Context, access);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetMilestonesAsync(1, "emp-2", 1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetTimelineAsync(1, "emp-2", 1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetGalleryAsync(1, "emp-2", 1));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetProjectProfitAndLossAsync(1, "emp-2", 1));
        }

        [Fact(DisplayName = "4. سود و زیان پروژه و مقایسه بودجه: احتساب فروش، برگشت از فروش، COGS، هزینه مستقیم و دستمزد تأییدشده")]
        public async Task ProjectProfitAndLoss_ComputesRevenueCogsExpensesWagesAndBudgetVariance()
        {
            var env = CreateEnv("ProjectPnL", "mgr-1");
            var access = new ProjectAccessService(env.Context);
            var svc = new ProjectProgressService(env.Context, access);

            env.Context.SalesInvoices.Add(new SalesInvoice
            {
                Id = 1,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                SellerUserId = "mgr-1",
                Number = "S-1001",
                IssueDate = new DateTime(2026, 4, 15),
                Status = InvoiceStatus.Final,
                Subtotal = 200_000_000m,
                Total = 200_000_000m,
                Description = "مرحله اول تجهیزات",
                CreatedAt = DateTime.UtcNow,
                Items = new List<SalesInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 1, Quantity = 1, UnitPriceSell = 150_000_000m, LineTotal = 150_000_000m, UnitCost = 70_000_000m, TotalCost = 70_000_000m, CreatedAt = DateTime.UtcNow },
                    new() { TenantId = 1, ProductId = 2, Quantity = 100, UnitPriceSell = 500_000m, LineTotal = 50_000_000m, UnitCost = 200_000m, TotalCost = 20_000_000m, CreatedAt = DateTime.UtcNow }
                }
            });

            env.Context.SalesInvoices.Add(new SalesInvoice
            {
                Id = 2,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                SellerUserId = "mgr-1",
                Number = "PF-1002",
                IssueDate = new DateTime(2026, 4, 20),
                Status = InvoiceStatus.Proforma,
                Subtotal = 80_000_000m,
                Total = 80_000_000m,
                Description = "پیش‌فاکتور الحاقی",
                CreatedAt = DateTime.UtcNow
            });

            env.Context.SalesReturnInvoices.Add(new SalesReturnInvoice
            {
                Id = 1,
                TenantId = 1,
                FiscalYearId = 1,
                WarehouseId = 1,
                ProjectId = 1,
                SellerUserId = "mgr-1",
                Number = "SR-1001",
                IssueDate = new DateTime(2026, 4, 25),
                Status = InvoiceStatus.Final,
                Subtotal = 10_000_000m,
                Total = 10_000_000m,
                CreatedAt = DateTime.UtcNow,
                Items = new List<SalesReturnInvoiceItem>
                {
                    new() { TenantId = 1, ProductId = 2, Quantity = 20, UnitPriceReturn = 500_000m, LineTotal = 10_000_000m, UnitCost = 200_000m, TotalCost = 4_000_000m, CreatedAt = DateTime.UtcNow }
                }
            });

            env.Context.Expenses.Add(new Expense
            {
                Id = 1,
                TenantId = 1,
                FiscalYearId = 1,
                ProjectId = 1,
                ExpenseTitle = "جرثقیل و حمل موتور",
                Type = ExpenseType.Transport,
                ExpenseDate = new DateTime(2026, 4, 18),
                Amount = 15_000_000m,
                CreatedAt = DateTime.UtcNow
            });

            env.Context.ProjectWorkLogs.AddRange(
                new ProjectWorkLog
                {
                    Id = 1,
                    TenantId = 1,
                    FiscalYearId = 1,
                    ProjectId = 1,
                    UserId = "emp-1",
                    WorkDate = new DateTime(2026, 4, 19),
                    Hours = 10m,
                    HourlyRateSnapshot = 500_000m,
                    WageCost = 5_000_000m,
                    Description = "نصب شاسی",
                    Status = WorkLogStatus.Approved,
                    CreatedAt = DateTime.UtcNow
                },
                new ProjectWorkLog
                {
                    Id = 2,
                    TenantId = 1,
                    FiscalYearId = 1,
                    ProjectId = 1,
                    UserId = "emp-1",
                    WorkDate = new DateTime(2026, 4, 21),
                    Hours = 4m,
                    HourlyRateSnapshot = 500_000m,
                    WageCost = 2_000_000m,
                    Description = "در انتظار تأیید",
                    Status = WorkLogStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                },
                new ProjectWorkLog
                {
                    Id = 3,
                    TenantId = 1,
                    FiscalYearId = 1,
                    ProjectId = 1,
                    UserId = "emp-1",
                    WorkDate = new DateTime(2026, 4, 22),
                    Hours = 3m,
                    HourlyRateSnapshot = 500_000m,
                    WageCost = 1_500_000m,
                    Description = "رد شده",
                    Status = WorkLogStatus.Rejected,
                    CreatedAt = DateTime.UtcNow
                });

            await env.Context.SaveChangesAsync();

            var pnl = await svc.GetProjectProfitAndLossAsync(1, "mgr-1", 1);

            Assert.Equal(200_000_000m, pnl.GrossSalesRevenue);
            Assert.Equal(10_000_000m, pnl.SalesReturnsAmount);
            Assert.Equal(190_000_000m, pnl.NetSalesRevenue);
            Assert.Equal(80_000_000m, pnl.ProformaSalesTotal);

            Assert.Equal(86_000_000m, pnl.NetMaterialCogs);
            Assert.Equal(15_000_000m, pnl.TotalDirectExpenses);
            Assert.Equal(5_000_000m, pnl.TotalApprovedLaborCost);
            Assert.Equal(2_000_000m, pnl.PendingLaborCostEstimate);

            Assert.Equal(106_000_000m, pnl.TotalCosts);
            Assert.Equal(84_000_000m, pnl.NetProfit);

            Assert.Equal(120_000_000m, pnl.EstimatedBudget);
            Assert.Equal(14_000_000m, pnl.BudgetVariance);
            Assert.False(pnl.IsOverBudget);
            Assert.Equal(2, pnl.MaterialItems.Count);
            Assert.Single(pnl.LaborByMembers);
        }

        [Fact(DisplayName = "5. کنترلر پیشرفت پروژه: ثبت گزارش فرم‌دیتا با تصویر و واکشی گزارش سود و زیان با مجوز معتبر")]
        public async Task ProjectProgressController_AddLogAndGetProfitAndLoss_ReturnsOk()
        {
            var env = CreateEnv("ProgressController", "mgr-1");
            var access = new ProjectAccessService(env.Context);
            var svc = new ProjectProgressService(env.Context, access);

            var permMock = new Mock<IPermissionService>();
            permMock.Setup(p => p.HasPermissionAsync("mgr-1", It.IsAny<string>())).ReturnsAsync(true);

            var controller = new ProjectProgressController(svc, env.CurrentFy, permMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("TenantId", "1"),
                            new Claim("FiscalYearId", "1"),
                            new Claim(ClaimTypes.NameIdentifier, "mgr-1")
                        }, "TestAuth"))
                    }
                }
            };

            var contentBytes = Encoding.UTF8.GetBytes("PNG-CONTENT");
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("workshop.png");
            fileMock.Setup(f => f.ContentType).Returns("image/png");
            fileMock.Setup(f => f.Length).Returns(contentBytes.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns<Stream, CancellationToken>((s, _) => s.WriteAsync(contentBytes, 0, contentBytes.Length));

            var addResult = await controller.AddProgressLog(new ProjectProgressLogFormModel
            {
                ProjectId = 1,
                LogDate = "1405/02/15",
                Title = "گزارش تصویر کارگاهی",
                ReportText = "اتمام مرحله فونداسیون چاهک.",
                ProgressPercent = 35m,
                Files = new List<IFormFile> { fileMock.Object }
            });

            var okAdd = Assert.IsType<OkObjectResult>(addResult.Result);
            Assert.Equal(200, okAdd.StatusCode);

            var pnlResult = await controller.GetProfitAndLoss(1);
            var okPnl = Assert.IsType<OkObjectResult>(pnlResult.Result);
            Assert.Equal(200, okPnl.StatusCode);
        }
    }
}
