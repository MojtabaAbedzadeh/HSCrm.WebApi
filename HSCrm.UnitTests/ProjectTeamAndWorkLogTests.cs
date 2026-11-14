using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Seeders;
using HSCrm.DataModel.Services;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.StandardApiResult;
using HSCrm.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.UnitTests
{
    public class ProjectTeamAndWorkLogTests
    {
        private static (
            ApplicationDbContext Context,
            UnitOfWork Uow,
            MutableCurrentUserService CurrentUser,
            MutableCurrentFiscalYearService CurrentFiscalYear
        ) CreateEnvironment(string dbName, string currentUserId = "mgr-1")
        {
            var currentUser = new MutableCurrentUserService { TenantId = 1, UserId = currentUserId, IsAuthenticated = true };
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
                TenantTitle = "شرکت مهندسی آسانسور فاز دو",
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
                    UserLastName = "سیستم",
                    UserName = "manager1",
                    NormalizedUserName = "MANAGER1",
                    TenantId = 1,
                    IsTenantOwner = true,
                    UserStatus = true,
                    UserType = 4
                },
                new ApplicationUser
                {
                    Id = "emp-1",
                    UserFirstName = "کارمند",
                    UserLastName = "یکم",
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
                    UserLastName = "دوم",
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

            context.Projects.AddRange(
                new Project { Id = 1, TenantId = 1, ProjectTitle = "پروژه برج میلاد", Status = "Active", CreatedAt = DateTime.UtcNow },
                new Project { Id = 2, TenantId = 1, ProjectTitle = "پروژه برج سفید", Status = "Active", CreatedAt = DateTime.UtcNow });

            context.Warehouses.Add(new Warehouse { Id = 1, TenantId = 1, Title = "انبار مرکزی", IsActive = true, CreatedAt = DateTime.UtcNow });
            context.Suppliers.Add(new Supplier { Id = 1, TenantId = 1, SupplierFullName = "تأمین‌کننده قطعات", CreatedAt = DateTime.UtcNow });
            context.Products.Add(new Product { Id = 1, TenantId = 1, ProductTitle = "ریل آسانسور", ProductUnit = "متر", CreatedAt = DateTime.UtcNow });

            context.SaveChanges();
            return (context, new UnitOfWork(context), currentUser, currentFy);
        }

        private static ClaimsPrincipal CreatePrincipal(string userId, int tenantId = 1)
        {
            var claims = new List<Claim>
            {
                new("TenantId", tenantId.ToString()),
                new("FiscalYearId", "1"),
                new(ClaimTypes.NameIdentifier, userId)
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }

        [Fact(DisplayName = "1. تیم پروژه: افزودن چند کاربر به پروژه ۱، جلوگیری از تکرار، بازیابی پس از حذف و شمارش اعضا")]
        public async Task ProjectMembers_AddMultipleUpdateRemoveAndReactivate_WorksCorrectly()
        {
            var env = CreateEnvironment("ProjectMembersCrud");
            var access = new ProjectAccessService(env.Context);
            var teamSvc = new ProjectTeamService(env.Context, access);

            var m1 = await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "سرپرست نصب",
                HourlyRate = 500_000m,
                IsActive = true
            });
            var m2 = await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-2",
                ProjectRole = "تکنسین برق",
                HourlyRate = 350_000m,
                IsActive = true
            });

            Assert.Equal("سرپرست نصب", m1.ProjectRole);
            Assert.Equal(500_000m, m1.HourlyRate);
            Assert.Equal(2, (await teamSvc.GetProjectMembersAsync(1, 1)).Count);

            var counts = await teamSvc.GetProjectMemberCountsAsync(1, "mgr-1");
            Assert.Equal(2, counts[1]);

            // Upsert same user updates instead of creating duplicate
            var m1Updated = await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "مدیر فنی پروژه",
                HourlyRate = 650_000m,
                IsActive = true
            });
            Assert.Equal(m1.Id, m1Updated.Id);
            Assert.Equal("مدیر فنی پروژه", m1Updated.ProjectRole);
            Assert.Equal(650_000m, m1Updated.HourlyRate);

            // Remove emp-2 and verify count drops to 1, then re-add emp-2 seamlessly
            Assert.True(await teamSvc.RemoveMemberAsync(1, m2.Id, "mgr-1"));
            var countsAfterRemove = await teamSvc.GetProjectMemberCountsAsync(1, "mgr-1");
            Assert.Equal(1, countsAfterRemove[1]);

            var m2Restored = await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-2",
                ProjectRole = "تکنسین بازگشته",
                HourlyRate = 400_000m,
                IsActive = true
            });
            Assert.Equal(m2.Id, m2Restored.Id);
            Assert.True(m2Restored.IsActive);
            Assert.Equal(2, (await teamSvc.GetProjectMembersAsync(1, 1)).Count);
        }

        [Fact(DisplayName = "2. دسترسی پروژه‌محور کارمند: فقط پروژه‌های مجاز خود را می‌بیند و از دسترسی به سایر پروژه‌ها منع می‌شود")]
        public async Task ScopedEmployee_SeesOnlyAssignedProjects_AndForbiddenOnOthers()
        {
            var env = CreateEnvironment("ScopedProjectVisibility");
            var access = new ProjectAccessService(env.Context);
            var teamSvc = new ProjectTeamService(env.Context, access);

            await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "نصاب",
                IsActive = true
            });

            var projectController = new ProjectController(env.Uow, access)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal("emp-1") }
                }
            };

            var listRes = await projectController.Get();
            Assert.True(listRes.Status);
            var projects = listRes.Data!.ToList();
            Assert.Single(projects);
            Assert.Equal(1, projects[0].Id);

            var dropdownRes = await projectController.ProjectDropdownList();
            var ddItems = Assert.IsAssignableFrom<IEnumerable<DropdownDto>>(((ApiResponse<IEnumerable<DropdownDto>>)dropdownRes).Data);
            Assert.Single(ddItems);
            Assert.Equal(1, ddItems.First().DrId);

            var forbiddenRes = await projectController.GetById(2);
            Assert.False(forbiddenRes.Status);
            Assert.Equal(ApiResultStatusCode.Forbidden, forbiddenRes.StatusCode);

            // Manager sees both projects
            projectController.ControllerContext.HttpContext.User = CreatePrincipal("mgr-1");
            var mgrList = await projectController.Get();
            Assert.Equal(2, mgrList.Data!.Count());
        }

        [Fact(DisplayName = "3. ثبت هزینه و فاکتور فروش توسط کارمند: فقط روی پروژه مجاز موفق است و روی پروژه غیرمجاز ۴۰۳ برمی‌گرداند")]
        public async Task ScopedEmployee_CanCreateExpenseAndSalesInvoiceOnlyOnAssignedProject()
        {
            var env = CreateEnvironment("ScopedExpenseAndSales");
            var access = new ProjectAccessService(env.Context);
            var teamSvc = new ProjectTeamService(env.Context, access);

            await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "کارشناس پروژه",
                IsActive = true
            });

            var accMock = new Mock<IAccountingService>();
            var settleMock = new Mock<IPaymentSettlementService>();
            var invMock = new Mock<IInventoryService>();

            var expenseController = new ExpenseController(env.Uow, settleMock.Object, env.CurrentFiscalYear, accMock.Object, access)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal("emp-1") }
                }
            };

            var forbiddenExp = await expenseController.Create(new ExpenseCreateModel
            {
                ExpenseTitle = "هزینه غیرمجاز",
                ProjectId = 2,
                Type = ExpenseType.Transport,
                ExpenseDate = "1405/08/20",
                Amount = 1_000_000m
            });
            Assert.False(forbiddenExp.Status);
            Assert.Equal(ApiResultStatusCode.Forbidden, forbiddenExp.StatusCode);

            var okExp = await expenseController.Create(new ExpenseCreateModel
            {
                ExpenseTitle = "کرایه حمل تجهیزات",
                ProjectId = 1,
                Type = ExpenseType.Transport,
                ExpenseDate = "1405/08/20",
                Amount = 1_500_000m
            });
            Assert.True(okExp.Status);

            var salesController = new SalesInvoiceController(env.Uow, invMock.Object, settleMock.Object, accMock.Object, env.CurrentFiscalYear, null, access)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal("emp-1") }
                }
            };

            var forbiddenSales = await salesController.CreateSalesInvoice(new SalesInvoiceCreateModel
            {
                ProjectId = 2,
                WarehouseId = 1,
                IssueDate = "1405/08/20",
                SellerUserId = "emp-1",
                Status = InvoiceStatus.Proforma,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 2, UnitPrice = 500_000m }
                }
            });
            Assert.False(forbiddenSales.Status);
            Assert.Equal(ApiResultStatusCode.Forbidden, forbiddenSales.StatusCode);

            var okSales = await salesController.CreateSalesInvoice(new SalesInvoiceCreateModel
            {
                ProjectId = 1,
                WarehouseId = 1,
                IssueDate = "1405/08/20",
                SellerUserId = "emp-1",
                Status = InvoiceStatus.Proforma,
                Items = new List<SalesInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 2, UnitPrice = 500_000m }
                }
            });
            Assert.True(okSales.Status);
        }

        [Fact(DisplayName = "4. فاکتور خرید توسط کارمند دارای مجوز: ثبت استاندارد با تأمین‌کننده و انبار (بدون مشتری و پروژه)")]
        public async Task ScopedEmployee_WithPurchaseCreate_CreatesStandardPurchaseInvoiceWithoutProjectOrCustomer()
        {
            var env = CreateEnvironment("ScopedPurchaseInvoice");
            var invMock = new Mock<IInventoryService>();
            var accMock = new Mock<IAccountingService>();
            var settleMock = new Mock<IPaymentSettlementService>();

            var purchaseController = new PurchaseInvoiceController(
                env.Uow,
                invMock.Object,
                env.CurrentFiscalYear,
                settleMock.Object,
                NullLogger<PurchaseInvoiceController>.Instance,
                accMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = CreatePrincipal("emp-1") }
                }
            };

            var res = await purchaseController.Create(new PurchaseInvoiceCreateModel
            {
                SupplierId = 1,
                WarehouseId = 1,
                TenantId = 1,
                PurchaserId = "emp-1",
                IssueDate = "1405/08/20",
                Status = InvoiceStatus.Proforma,
                Items = new List<PurchaseInvoiceItemCreateModel>
                {
                    new() { ProductId = 1, Quantity = 10, UnitPrice = 200_000m }
                }
            });

            Assert.True(res.Status);
            var saved = await env.Context.PurchaseInvoices.FirstOrDefaultAsync();
            Assert.NotNull(saved);
            Assert.Equal(1, saved!.SupplierId);
            Assert.Equal(1, saved.WarehouseId);
            Assert.Equal(2_000_000m, saved.Total);
        }

        [Fact(DisplayName = "5. چرخه ثبت و تأیید ساعات کاری (Timesheet): محاسبه دستمزد، منع ویرایش بعد از تأیید، تأیید و رد مدیر")]
        public async Task ProjectWorkLog_CreateEditApproveRejectAndWageCalculation_WorksCorrectly()
        {
            var env = CreateEnvironment("WorkLogLifecycle");
            var access = new ProjectAccessService(env.Context);
            var teamSvc = new ProjectTeamService(env.Context, access);

            await teamSvc.AddOrUpdateMemberAsync(1, "mgr-1", new ProjectMemberUpsertModel
            {
                ProjectId = 1,
                UserId = "emp-1",
                ProjectRole = "نصاب",
                HourlyRate = 400_000m,
                IsActive = true
            });

            // Cannot log on unassigned project 2
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                teamSvc.CreateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogCreateModel
                {
                    ProjectId = 2,
                    WorkDate = "1405/08/20",
                    Hours = 4m,
                    Description = "تلاش روی پروژه غیرمجاز"
                }));

            // Log on assigned project 1 -> Pending with WageCost = 6 * 400,000 = 2,400,000
            var log1 = await teamSvc.CreateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogCreateModel
            {
                ProjectId = 1,
                WorkDate = "1405/08/20",
                Hours = 6m,
                Description = "نصب ریل راهنما طبقه اول تا سوم"
            });
            Assert.Equal(WorkLogStatus.Pending, log1.Status);
            Assert.Equal(2_400_000m, log1.WageCost);

            // Edit while Pending -> allowed
            var edited = await teamSvc.UpdateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogEditModel
            {
                Id = log1.Id,
                ProjectId = 1,
                WorkDate = "1405/08/21",
                Hours = 8m,
                Description = "نصب ریل راهنما (اصلاح ساعت)"
            });
            Assert.Equal(3_200_000m, edited.WageCost);

            // Manager approves with overridden hourly rate 450,000 -> WageCost = 3,600,000
            var approved = await teamSvc.ReviewWorkLogAsync(1, 1, "mgr-1", new ProjectWorkLogReviewModel
            {
                WorkLogId = log1.Id,
                IsApproved = true,
                ReviewNote = "تایید شد",
                OverrideHourlyRate = 450_000m
            });
            Assert.Equal(WorkLogStatus.Approved, approved.Status);
            Assert.Equal(3_600_000m, approved.WageCost);

            // Cannot edit or delete after Approved
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                teamSvc.UpdateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogEditModel
                {
                    Id = log1.Id,
                    ProjectId = 1,
                    WorkDate = "1405/08/21",
                    Hours = 5m,
                    Description = "تغییر پس از تایید"
                }));

            // Reject requires reason, then works cleanly
            var log2 = await teamSvc.CreateWorkLogAsync(1, 1, "emp-1", new ProjectWorkLogCreateModel
            {
                ProjectId = 1,
                WorkDate = "1405/08/22",
                Hours = 3m,
                Description = "کار اضافه"
            });
            await Assert.ThrowsAsync<ArgumentException>(() =>
                teamSvc.ReviewWorkLogAsync(1, 1, "mgr-1", new ProjectWorkLogReviewModel
                {
                    WorkLogId = log2.Id,
                    IsApproved = false,
                    ReviewNote = ""
                }));

            var rejected = await teamSvc.ReviewWorkLogAsync(1, 1, "mgr-1", new ProjectWorkLogReviewModel
            {
                WorkLogId = log2.Id,
                IsApproved = false,
                ReviewNote = "فاقد گزارش سرپرست"
            });
            Assert.Equal(WorkLogStatus.Rejected, rejected.Status);

            var dash = await teamSvc.GetEmployeeDashboardAsync(1, 1, "emp-1");
            Assert.Equal(1, dash.AssignedProjectsCount);
            Assert.Equal(8m, dash.ApprovedHours);
            Assert.Equal(3m, dash.RejectedHours);
            Assert.Equal(3_600_000m, dash.ApprovedWageCost);
        }

        [Fact(DisplayName = "6. سید دسترسی‌های فاز ۲: مجوزهای ProjectMembers و WorkLogs ایجاد و به نقش‌ها تخصیص می‌یابند")]
        public async Task PermissionSeeder_SeedsPhase2Permissions_ForAdminOwnerAndBaselineUser()
        {
            var env = CreateEnvironment("Phase2PermissionsSeed");
            await PermissionSeeder.SeedAsync(env.Context);

            // شبیه‌سازی سناریویی که نقش User از قبل دسترسی دیگری (مثلاً customers.view) داشته است
            var existingOtherPerm = await env.Context.Permissions.FirstOrDefaultAsync(p => p.Name == "customers.view");
            if (existingOtherPerm != null)
            {
                env.Context.RolePermissions.Add(new RolePermission { RoleId = "role-user", PermissionId = existingOtherPerm.Id });
                await env.Context.SaveChangesAsync();
            }

            await RoleSeeder.AssignPermissionsToRoles(env.Context);
            await env.Context.SaveChangesAsync();

            var seededNames = await env.Context.Permissions.Select(p => p.Name).ToListAsync();
            Assert.Contains("projectMembers.view", seededNames);
            Assert.Contains("projectMembers.manage", seededNames);
            Assert.Contains("workLogs.view", seededNames);
            Assert.Contains("workLogs.create", seededNames);
            Assert.Contains("workLogs.approve", seededNames);

            var permSvc = new PermissionService(env.Context);
            Assert.True(await permSvc.HasPermissionAsync("mgr-1", "projectMembers.manage"));
            Assert.True(await permSvc.HasPermissionAsync("mgr-1", "workLogs.approve"));
            Assert.True(await permSvc.HasPermissionAsync("emp-1", "workLogs.create"));
            Assert.False(await permSvc.HasPermissionAsync("emp-1", "workLogs.approve"));
            Assert.False(await permSvc.HasPermissionAsync("emp-1", "purchaseInvoices.create"));
        }
    }
}
