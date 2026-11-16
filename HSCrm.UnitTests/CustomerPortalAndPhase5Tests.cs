using System.Security.Claims;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using AutoMapper;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class CustomerPortalAndPhase5Tests
    {
        private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationDbContext context)
        {
            var store = new Mock<IUserStore<ApplicationUser>>();
            var mgr = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            mgr.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((string email) => context.Users.FirstOrDefault(u => u.Email == email));

            mgr.Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
                .ReturnsAsync((ApplicationUser u) =>
                {
                    context.Users.Update(u);
                    context.SaveChanges();
                    return IdentityResult.Success;
                });

            mgr.Setup(m => m.ChangePasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser u, string currentPwd, string newPwd) =>
                    currentPwd == "Old@12345"
                        ? IdentityResult.Success
                        : IdentityResult.Failed(new IdentityError { Description = "رمز عبور فعلی اشتباه است." }));

            return mgr;
        }

        private static (ApplicationDbContext Context, MutableCurrentUserService CurrentUser, MutableCurrentFiscalYearService CurrentFy) CreateEnv(string dbName, string userId = "cust-user-1")
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
                TenantTitle = "شرکت آسانسور پارس",
                TaxId = "10101234567",
                CompanyPhone = "02188889999",
                Address = "تهران، خیابان ولیعصر",
                InvoiceFooterText = "گارانتی ۱۲ ماهه قطعات و نصب",
                LogoUrl = "/Upload/Logos/pars.png",
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

            context.Roles.AddRange(
                new ApplicationRole { Id = "r-owner", Name = "Owner", NormalizedName = "OWNER", RoleFaName = "مدیر", TenantId = 1 },
                new ApplicationRole { Id = "r-cust", Name = "Customer", NormalizedName = "CUSTOMER", RoleFaName = "مشتری", TenantId = 1 },
                new ApplicationRole { Id = "r-user", Name = "User", NormalizedName = "USER", RoleFaName = "کاربر", TenantId = 1 }
            );

            context.Users.AddRange(
                new ApplicationUser
                {
                    Id = "mgr-1",
                    UserFirstName = "مدیر",
                    UserLastName = "سیستم",
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
                    UserLastName = "نصاب",
                    UserName = "emp1",
                    NormalizedUserName = "EMP1",
                    TenantId = 1,
                    IsTenantOwner = false,
                    UserStatus = true,
                    UserType = 0
                },
                new ApplicationUser
                {
                    Id = "cust-user-1",
                    UserFirstName = "علی",
                    UserLastName = "رضایی",
                    UserName = "ali_customer",
                    NormalizedUserName = "ALI_CUSTOMER",
                    Email = "ali@example.com",
                    PhoneNumber = "09121112233",
                    TenantId = 1,
                    IsTenantOwner = false,
                    UserStatus = true,
                    UserType = 3
                },
                new ApplicationUser
                {
                    Id = "cust-user-2",
                    UserFirstName = "سارا",
                    UserLastName = "محمدی",
                    UserName = "sara_customer",
                    NormalizedUserName = "SARA_CUSTOMER",
                    TenantId = 1,
                    IsTenantOwner = false,
                    UserStatus = true,
                    UserType = 3
                }
            );

            context.UserRoles.AddRange(
                new UserRole { UserId = "mgr-1", RoleId = "r-owner" },
                new UserRole { UserId = "cust-user-1", RoleId = "r-cust" },
                new UserRole { UserId = "cust-user-2", RoleId = "r-cust" },
                new UserRole { UserId = "emp-1", RoleId = "r-user" }
            );

            var cust1 = new Customer { Id = 1, TenantId = 1, Name = "علی رضایی", Phone = "09121112233", Email = "ali@example.com", Address = "تهران", ApplicationUserId = "cust-user-1", CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            var cust2 = new Customer { Id = 2, TenantId = 1, Name = "سارا محمدی", Phone = "09129998877", ApplicationUserId = "cust-user-2", CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            context.Customers.AddRange(cust1, cust2);

            var proj1 = new Project { Id = 10, TenantId = 1, ProjectTitle = "آسانسور برج بهار", CustomerId = 1, ProjectLocation = "تهران", Status = "Active", ProgressPercent = 65m, EstimatedBudget = 500000000m, CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            var proj2 = new Project { Id = 20, TenantId = 1, ProjectTitle = "آسانسور مجتمع لاله", CustomerId = 2, ProjectLocation = "کرج", Status = "Active", ProgressPercent = 20m, CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            context.Projects.AddRange(proj1, proj2);

            context.ProjectMilestones.Add(new ProjectMilestone
            {
                Id = 100,
                TenantId = 1,
                ProjectId = 10,
                Title = "آهن‌کشی چاهک",
                SortOrder = 1,
                WeightPercent = 50m,
                ProgressPercent = 100m,
                Status = MilestoneStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                CreatedById = "mgr-1"
            });

            context.ProjectProgressLogs.Add(new ProjectProgressLog
            {
                Id = 200,
                TenantId = 1,
                ProjectId = 10,
                MilestoneId = 100,
                FiscalYearId = 1,
                RecordedByUserId = "emp-1",
                LogDate = DateTime.UtcNow.AddDays(-2),
                Title = "نصب ریل راهنما",
                ReportText = "ریل‌های کابین و وزنه با موفقیت نصب و تراز شد.",
                ProgressPercent = 65m,
                WorkshopArea = "طبقه اول تا پنجم",
                CreatedAt = DateTime.UtcNow,
                CreatedById = "emp-1"
            });

            var wh = new Warehouse { Id = 1, TenantId = 1, Title = "انبار مرکزی", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            var prod = new Product { Id = 1, TenantId = 1, ProductTitle = "موتور گیرلس", ProductUnit = "دستگاه", CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" };
            context.Warehouses.Add(wh);
            context.Products.Add(prod);

            context.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    Id = 300,
                    TenantId = 1,
                    FiscalYearId = 1,
                    WarehouseId = 1,
                    SellerUserId = "mgr-1",
                    ProjectId = 10,
                    Number = "INV-1405-0001",
                    IssueDate = DateTime.UtcNow.AddDays(-1),
                    Status = InvoiceStatus.Final,
                    Subtotal = 100000000m,
                    Tax = 9000000m,
                    Discount = 0m,
                    Total = 109000000m,
                    Description = "مرحله اول تجهیزات",
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = "mgr-1",
                    Items = new List<SalesInvoiceItem>
                    {
                        new SalesInvoiceItem
                        {
                            Id = 301,
                            TenantId = 1,
                            ProductId = 1,
                            Quantity = 1,
                            UnitPriceSell = 100000000m,
                            Tax = 9000000m,
                            Discount = 0m,
                            LineTotal = 109000000m,
                            CreatedAt = DateTime.UtcNow,
                            CreatedById = "mgr-1"
                        }
                    }
                },
                new SalesInvoice
                {
                    Id = 302,
                    TenantId = 1,
                    FiscalYearId = 1,
                    WarehouseId = 1,
                    SellerUserId = "mgr-1",
                    ProjectId = 20,
                    Number = "INV-1405-0002",
                    IssueDate = DateTime.UtcNow,
                    Status = InvoiceStatus.Final,
                    Subtotal = 50000000m,
                    Tax = 0m,
                    Discount = 0m,
                    Total = 50000000m,
                    Description = "فاکتور مشتری دیگر",
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = "mgr-1"
                }
            );

            context.Receipts.Add(new Receipt
            {
                Id = 400,
                TenantId = 1,
                FiscalYearId = 1,
                CustomerId = 1,
                SalesInvoiceId = 300,
                ReceiptDate = DateTime.UtcNow,
                Amount = 40000000m,
                Method = "Transfer",
                ReferenceNo = "TR-9988",
                Description = "پیش‌پرداخت",
                CreatedAt = DateTime.UtcNow,
                CreatedById = "mgr-1"
            });

            context.SaveChanges();
            return (context, currentUser, currentFy);
        }

        [Fact(DisplayName = "1. پرتال مشتری: ایزوله‌سازی کامل پروژه‌ها و فاکتورها برای مشتری جاری")]
        public async Task CustomerPortal_IsolatesProjectsAndInvoicesStrictlyToCurrentCustomer()
        {
            var (context, _, _) = CreateEnv(nameof(CustomerPortal_IsolatesProjectsAndInvoicesStrictlyToCurrentCustomer));
            var um = CreateUserManagerMock(context);
            var service = new CustomerPortalService(context, um.Object);

            var projects = await service.GetProjectsAsync(1, "cust-user-1");
            Assert.Single(projects);
            Assert.Equal(10, projects[0].Id);
            Assert.Equal("آسانسور برج بهار", projects[0].ProjectTitle);
            Assert.Equal(65m, projects[0].ProgressPercent);

            // Access to customer 2's project must fail
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetProjectDetailAsync(1, "cust-user-1", 20));

            var invoices = await service.GetInvoicesAsync(1, "cust-user-1");
            Assert.Single(invoices);
            Assert.Equal("INV-1405-0001", invoices[0].Number);
            Assert.Equal("شرکت آسانسور پارس", invoices[0].CompanyInfo.CompanyTitle);
            Assert.Equal("گارانتی 12 ماهه قطعات و نصب", invoices[0].CompanyInfo.InvoiceFooterText);
            Assert.Equal("10101234567", invoices[0].CompanyInfo.TaxId);

            // Access to customer 2's invoice must fail
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetInvoiceByIdAsync(1, "cust-user-1", 302));
        }

        [Fact(DisplayName = "2. پرتال مشتری: صورت‌حساب ۳۶۰، ویرایش پروفایل و تغییر رمز عبور مشتری")]
        public async Task CustomerPortal_StatementAndProfileAndPasswordChangeWorkCleanly()
        {
            var (context, _, _) = CreateEnv(nameof(CustomerPortal_StatementAndProfileAndPasswordChangeWorkCleanly));
            var um = CreateUserManagerMock(context);
            var service = new CustomerPortalService(context, um.Object);

            var dash = await service.GetDashboardAsync(1, "cust-user-1");
            Assert.Equal(1, dash.TotalProjectsCount);
            Assert.Equal(109000000m, dash.TotalInvoicesAmount);
            Assert.Equal(69000000m, dash.NetOpenBalance); // 109M - 40M receipt

            var stmt = await service.GetStatementAsync(1, "cust-user-1");
            Assert.Single(stmt.Receipts);
            Assert.Equal(40000000m, stmt.Receipts[0].Amount);

            var updated = await service.UpdateProfileAsync(1, "cust-user-1", new CustomerPortalProfileUpdateModel
            {
                UserFirstName = "علیرضا",
                UserLastName = "رضایی",
                Phone = "09123334455",
                Email = "alireza@example.com",
                Address = "تهران، ونک"
            });
            Assert.Equal("علیرضا رضایی", updated.FullName);
            Assert.Equal("09123334455", context.Customers.First(c => c.Id == 1).Phone);

            var badPwd = await service.ChangePasswordAsync(1, "cust-user-1", new CustomerPortalChangePasswordModel
            {
                CurrentPassword = "Wrong",
                NewPassword = "New@12345",
                ConfirmPassword = "New@12345"
            });
            Assert.False(badPwd.Succeeded);

            var goodPwd = await service.ChangePasswordAsync(1, "cust-user-1", new CustomerPortalChangePasswordModel
            {
                CurrentPassword = "Old@12345",
                NewPassword = "New@12345",
                ConfirmPassword = "New@12345"
            });
            Assert.True(goodPwd.Succeeded);
        }

        [Fact(DisplayName = "3. مرکز اعلان‌ها: صدور خودکار اعلان هنگام ثبت/تایید ساعت کار، تغییر وضعیت پروژه و سررسید چک")]
        public async Task NotificationCenter_GeneratesAndMarksReadForWorkLogProjectAndCheques()
        {
            var (context, _, _) = CreateEnv(nameof(NotificationCenter_GeneratesAndMarksReadForWorkLogProjectAndCheques), "mgr-1");
            context.ProjectMembers.Add(new ProjectMember
            {
                Id = 1,
                TenantId = 1,
                ProjectId = 10,
                UserId = "emp-1",
                HourlyRate = 500000m,
                IsActive = true,
                JoinedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedById = "mgr-1"
            });
            context.Cheques.Add(new Cheque
            {
                Id = 1,
                TenantId = 1,
                FiscalYearId = 1,
                Type = ChequeType.Receivable,
                Status = ChequeStatus.InSafe,
                ChequeNumber = "CHQ-8899",
                Amount = 25000000m,
                IssueDate = DateTime.UtcNow.AddDays(-5),
                DueDate = DateTime.UtcNow.AddDays(3),
                BankName = "ملت",
                CreatedAt = DateTime.UtcNow,
                CreatedById = "mgr-1"
            });
            context.SaveChanges();

            var notifService = new NotificationService(context);
            var accessService = new ProjectAccessService(context);
            var teamService = new ProjectTeamService(context, accessService, notifService);

            var createdLog = await teamService.CreateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogCreateModel
            {
                ProjectId = 10,
                WorkDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Hours = 4m,
                Description = "نصب تابلو فرمان"
            });

            // Manager should see worklog submission + upcoming cheque alert
            var mgrFeed = await notifService.GetMyNotificationsAsync(1, "mgr-1", includeChequeAlerts: true);
            Assert.Contains(mgrFeed.Items, n => n.Category == "WorkLog" && n.ReferenceId == createdLog.Id);
            Assert.Contains(mgrFeed.Items, n => n.Category == "Cheque" && n.ReferenceId == 1);

            // Employee review triggers notification to emp-1, and emp-1 without cheque perm sees no cheques
            await teamService.ReviewWorkLogAsync(1, 1, "mgr-1", new ProjectWorkLogReviewModel
            {
                WorkLogId = createdLog.Id,
                IsApproved = true,
                ReviewNote = "عالی"
            });

            var project = context.Projects.First(p => p.Id == 10);
            await notifService.NotifyProjectStatusChangedAsync(1, project, "Active", "Completed", "mgr-1", "تحویل موقت");

            var empFeed = await notifService.GetMyNotificationsAsync(1, "emp-1", includeChequeAlerts: false);
            Assert.DoesNotContain(empFeed.Items, n => n.Category == "Cheque");
            Assert.Contains(empFeed.Items, n => n.Category == "WorkLog" && n.Severity == "success");
            Assert.Contains(empFeed.Items, n => n.Category == "Project");
            Assert.True(empFeed.UnreadCount >= 2);

            // Customer also receives project status change notification
            var custFeed = await notifService.GetMyNotificationsAsync(1, "cust-user-1", includeChequeAlerts: false);
            Assert.Contains(custFeed.Items, n => n.Category == "Project" && n.ActionUrl == "/UserArea/Project/Details/10");

            var markedAll = await notifService.MarkAllAsReadAsync(1, "emp-1");
            Assert.True(markedAll >= 2);
            var empFeedAfter = await notifService.GetMyNotificationsAsync(1, "emp-1", includeChequeAlerts: false);
            Assert.Equal(0, empFeedAfter.UnreadCount);
        }

        [Fact(DisplayName = "4. تاریخچه تغییرات (Audit Trail): بازیابی ایجادکننده و ویرایش‌کننده اسناد با ایزوله‌سازی تننت")]
        public async Task AuditTrail_ReturnsCreatedAndModifiedMetadataScopedByTenant()
        {
            var (context, currentUser, _) = CreateEnv(nameof(AuditTrail_ReturnsCreatedAndModifiedMetadataScopedByTenant), "mgr-1");
            var proj = context.Projects.First(p => p.Id == 10);
            currentUser.UserId = "emp-1";
            proj.ModifiedAt = DateTime.UtcNow;
            proj.ModifiedById = "emp-1";
            context.SaveChanges();

            // Other tenant data must never leak
            currentUser.UserId = "mgr-1";
            context.Customers.Add(new Customer { Id = 99, TenantId = 2, Name = "مشتری تننت دیگر", CreatedAt = DateTime.UtcNow, CreatedById = "mgr-1" });
            context.SaveChanges();

            var auditService = new AuditTrailService(context);
            var logs = await auditService.GetAuditLogsAsync(1, entityType: "Project");
            Assert.Equal(2, logs.Items.Count);
            var proj10Log = Assert.Single(logs.Items, l => l.EntityId == 10);
            Assert.Equal("مدیر سیستم", proj10Log.CreatedByName);
            Assert.Equal("مهندس نصاب", proj10Log.ModifiedByName);

            var allTenant1 = await auditService.GetAuditLogsAsync(1);
            Assert.DoesNotContain(allTenant1.Items, i => i.EntityType == "Customer" && i.EntityId == 99);

            var singleHist = await auditService.GetEntityHistoryAsync(1, "SalesInvoice", 300);
            Assert.NotNull(singleHist);
            Assert.Equal("فاکتور #INV-1405-0001", singleHist!.DocumentNumberOrTitle);
        }

        [Fact(DisplayName = "5. ریست رمز عبور کاربر: بازنشانی رمز عبور کاربر همان شرکت و منع دسترسی متقاطع بین شرکت‌ها")]
        public async Task UserController_ResetPassword_ResetsSameTenantAndIsolatesCrossTenant()
        {
            var (context, _, _) = CreateEnv(nameof(UserController_ResetPassword_ResetsSameTenantAndIsolatesCrossTenant), "mgr-1");
            var empTenant1 = context.Users.First(u => u.Id == "emp-1");
            var empTenant2 = new ApplicationUser
            {
                Id = "emp-t2",
                TenantId = 2,
                UserFirstName = "کاربر",
                UserLastName = "دیگر",
                UserName = "emp_t2",
                NormalizedUserName = "EMP_T2",
                UserStatus = true,
                UserType = 0
            };
            context.Users.Add(empTenant2);
            context.SaveChanges();

            var store = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
            userManagerMock.Setup(m => m.FindByIdAsync("emp-1")).ReturnsAsync(empTenant1);
            userManagerMock.Setup(m => m.FindByIdAsync("emp-t2")).ReturnsAsync(empTenant2);
            userManagerMock.Setup(m => m.GeneratePasswordResetTokenAsync(empTenant1)).ReturnsAsync("tok-1");
            userManagerMock.Setup(m => m.ResetPasswordAsync(empTenant1, "tok-1", "NewPass@123")).ReturnsAsync(IdentityResult.Success);
            userManagerMock.Setup(m => m.IsLockedOutAsync(empTenant1)).ReturnsAsync(false);
            userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(empTenant1)).ReturnsAsync(IdentityResult.Success);

            var controller = new UserController(
                new UnitOfWork(context),
                userManagerMock.Object,
                new Mock<IMapper>().Object,
                context,
                new Mock<IRoleService>().Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                        {
                            new Claim("TenantId", "1"),
                            new Claim(ClaimTypes.NameIdentifier, "mgr-1"),
                            new Claim(ClaimTypes.Role, "Owner")
                        }, "TestAuth"))
                    }
                }
            };

            var crossTenant = await controller.ResetPassword(new UserResetPasswordModel
            {
                UserId = "emp-t2",
                NewPassword = "NewPass@123"
            });
            Assert.IsType<NotFoundObjectResult>(crossTenant.Result);

            var sameTenant = await controller.ResetPassword(new UserResetPasswordModel
            {
                UserId = "emp-1",
                NewPassword = "NewPass@123"
            });
            var ok = Assert.IsType<OkObjectResult>(sameTenant.Result);
            var payload = Assert.IsType<HSCrm.Framework.StandardApiResult.ApiResponse>(ok.Value);
            Assert.True(payload.Status);
            userManagerMock.Verify(m => m.ResetPasswordAsync(empTenant1, "tok-1", "NewPass@123"), Times.Once);
        }
    }
}
