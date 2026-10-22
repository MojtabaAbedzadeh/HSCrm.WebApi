using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Linq.Expressions;
using System.Reflection;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string, IdentityUserClaim<string>, UserRole, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly ICurrentFiscalYearService _currentFiscalYearService;
        public int CurrentTenantId => _currentUserService?.TenantId ?? 0;
        public int? CurrentFiscalYearId => _currentFiscalYearService?.FiscalYearId;
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService, ICurrentFiscalYearService currentFiscalYearService) : base(options)
        {
            _currentUserService = currentUserService;
            _currentFiscalYearService = currentFiscalYearService;
        }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductPrice> ProductPrices { get; set; }
        public DbSet<PurchaseInvoice> PurchaseInvoices { get; set; }
        public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems { get; set; }
        public DbSet<SalesInvoice> SalesInvoices { get; set; }
        public DbSet<SalesInvoiceItem> SalesInvoiceItems { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Warehouse> Warehouses { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<AccountingAccount> AccountingAccounts { get; set; }
        public DbSet<JournalVoucher> JournalVouchers { get; set; }
        public DbSet<JournalVoucherItem> JournalVoucherItems { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<Cheque> Cheques { get; set; }
        public DbSet<SalesReturnInvoice> SalesReturnInvoices { get; set; }
        public DbSet<SalesReturnInvoiceItem> SalesReturnInvoiceItems { get; set; }
        public DbSet<PurchaseReturnInvoice> PurchaseReturnInvoices { get; set; }
        public DbSet<PurchaseReturnInvoiceItem> PurchaseReturnInvoiceItems { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            ApplyFilters(builder);

            var adminUserId = "28dfc395-0464-4b47-8aae-c73953a9d153";
            var adminRoleId = "AdminId";
            var userRoleId = "UserId";
            var ownerRoleId = "OwnerId";
            var customerId = "CustomerId";
            var defaultTenantId = 1;

            // ======================= Tenant =======================
            builder.Entity<Tenant>().HasData(
                new Tenant
                {
                    Id = defaultTenantId,
                    TenantCode = "Central",
                    TenantTitle = "سیستم مرکزی",
                    Currency = "IRR",
                    CreatedAt = new DateTime(2024, 1, 1), 
                    IsDeleted = false
                }
            );

            builder.Entity<Tenant>().HasIndex(t => t.TenantCode).IsUnique();

            // ======================= Roles =======================
            builder.Entity<ApplicationRole>().HasData(
                new ApplicationRole
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    RoleFaName = "ادمین",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = adminRoleId,
                    TenantId = defaultTenantId
                },
                new ApplicationRole
                {
                    Id = ownerRoleId,
                    Name = "Owner",
                    RoleFaName = "مدیر",
                    NormalizedName = "OWNER",
                    ConcurrencyStamp = ownerRoleId,
                    TenantId = defaultTenantId
                },
                new ApplicationRole
                {
                    Id = customerId,
                    Name = "Customer",
                    RoleFaName = "مشتتری",
                    NormalizedName = "CUSTOMER",
                    ConcurrencyStamp = customerId,
                    TenantId = defaultTenantId
                },
                new ApplicationRole
                {
                    Id = userRoleId,
                    Name = "User",
                    RoleFaName = "کاربر",
                    NormalizedName = "USER",
                    ConcurrencyStamp = userRoleId,
                    TenantId = defaultTenantId
                }
            );

            // ======================= UserRole (Custom mapping) =======================
            builder.Entity<UserRole>(e =>
            {
                e.ToTable("UserRoles");
                e.HasKey(ur => new { ur.UserId, ur.RoleId });

                e.HasOne(ur => ur.User)
                    .WithMany(u => u.UserRoles)
                    .HasForeignKey(ur => ur.UserId);

                e.HasOne(ur => ur.Role)
                    .WithMany(r => r.UserRoles)
                    .HasForeignKey(ur => ur.RoleId);
            });

            // Seed Admin User to Admin Role
            builder.Entity<UserRole>().HasData(
                new UserRole { UserId = adminUserId, RoleId = adminRoleId },
                new UserRole { UserId = adminUserId, RoleId = ownerRoleId }
            );

            // ======================= Permission =======================
            builder.Entity<Permission>(e =>
            {
                e.ToTable("Permissions");
                e.HasIndex(p => p.Name).IsUnique();
                e.Property(p => p.Name).HasMaxLength(200).IsRequired();
                e.Property(p => p.Title).HasMaxLength(200).IsRequired();
                e.Property(p => p.CategoryTitleEn).HasMaxLength(100).IsRequired();
            });

            // ======================= RolePermission =======================
            builder.Entity<RolePermission>(e =>
            {
                e.ToTable("RolePermissions");
                e.HasKey(rp => new { rp.RoleId, rp.PermissionId });
                e.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
                e.HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions).HasForeignKey(rp => rp.PermissionId);
            });            

            // ======================= ApplicationUser Configuration =======================
            builder.Entity<ApplicationUser>(e =>
            {
                e.ToTable("Users");
                e.Property(x => x.Id).HasColumnName("UserId");

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.UserFirstName).IsRequired().HasMaxLength(100);
                e.Property(x => x.UserLastName).IsRequired().HasMaxLength(100);
                e.Property(x => x.UserImageUrl).HasMaxLength(500);
            });

            // Seed Admin User
            builder.Entity<ApplicationUser>().HasData(
                new ApplicationUser
                {
                    Id = adminUserId,
                    UserFirstName = "admin",
                    UserLastName = "admin",
                    UserName = "Admin",
                    NormalizedUserName = "ADMIN",
                    Email = "admin@example.com",
                    NormalizedEmail = "ADMIN@EXAMPLE.COM",
                    EmailConfirmed = true,
                    PhoneNumber = "09120000000",
                    UserImageUrl = "https://localhost:7096/Upload/UserImage/defaultAvatar.png",
                    TenantId = defaultTenantId,
                    IsTenantOwner = true,
                    UserStatus = true,
                    UserType = 110,
                    PasswordHash = "AQAAAAIAAYagAAAAEAsJ0rn1us3z/MyoCt4F9J63B6xi75XqeTzR+3b+aZPoajU3nTUfPjVQYV+ELmpG5g==",
                    SecurityStamp = "C674E456-6515-4D19-9B9D-9988D41C488F",
                    ConcurrencyStamp = "B1B2B3B4-B5B6-B7B8-B9B0-B1B2B3B4B5B6"
                }
            );

            builder.Entity<ApplicationRole>(e =>
            {
                e.ToTable("Roles");
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            });

            // ======================= Decimal Precision & Audited Entities =======================
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                    {
                        property.SetPrecision(18);
                        property.SetScale(2);
                    }
                }
            }

            // ======================= AuditedEntity Relations =======================
            ConfigureAuditedEntity<Tenant>(builder);
            ConfigureAuditedEntity<Customer>(builder);
            ConfigureAuditedEntity<Supplier>(builder);
            ConfigureAuditedEntity<Project>(builder);
            ConfigureAuditedEntity<Product>(builder);
            ConfigureAuditedEntity<ProductPrice>(builder);
            ConfigureAuditedEntity<PurchaseInvoice>(builder);
            ConfigureAuditedEntity<PurchaseInvoiceItem>(builder);
            ConfigureAuditedEntity<SalesInvoice>(builder);
            ConfigureAuditedEntity<SalesInvoiceItem>(builder);
            ConfigureAuditedEntity<Expense>(builder);
            ConfigureAuditedEntity<Payment>(builder);
            ConfigureAuditedEntity<FiscalYear>(builder);
            ConfigureAuditedEntity<Inventory>(builder);
            ConfigureAuditedEntity<InventoryTransaction>(builder);
            ConfigureAuditedEntity<Permission>(builder);
            ConfigureAuditedEntity<AccountingAccount>(builder);
            ConfigureAuditedEntity<JournalVoucher>(builder);
            ConfigureAuditedEntity<JournalVoucherItem>(builder);
            ConfigureAuditedEntity<BankAccount>(builder);
            ConfigureAuditedEntity<Cheque>(builder);
            ConfigureAuditedEntity<SalesReturnInvoice>(builder);
            ConfigureAuditedEntity<SalesReturnInvoiceItem>(builder);
            ConfigureAuditedEntity<PurchaseReturnInvoice>(builder);
            ConfigureAuditedEntity<PurchaseReturnInvoiceItem>(builder);

            // ======================= Tenant =======================
            builder.Entity<Tenant>(e =>
            {
                e.ToTable("Tenants");
                e.HasIndex(x => x.TenantTitle);
                e.Property(x => x.Currency).HasMaxLength(10);
            });

            // ======================= Customer =======================
            builder.Entity<Customer>(e =>
            {
                e.ToTable("Customers");
                e.HasIndex(x => new { x.TenantId, x.Name, x.IsDeleted });
                e.Property(x => x.Name).HasMaxLength(200);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ======================= Supplier =======================
            builder.Entity<Supplier>(e =>
            {
                e.ToTable("Suppliers");
                e.HasIndex(x => new { x.TenantId, x.SupplierFullName, x.IsDeleted });
                e.Property(x => x.SupplierFullName).HasMaxLength(200);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ======================= Project =======================
            builder.Entity<Project>(e =>
            {
                e.ToTable("Projects");
                e.HasIndex(x => new { x.TenantId, x.ProjectTitle, x.IsDeleted });
                e.Property(x => x.ProjectTitle).HasMaxLength(200);

                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ======================= Expense =======================
            builder.Entity<Payment>(entity =>
            {
                entity.HasOne(d => d.Expense)
                    .WithMany(p => p.Payments) // اگر در کلاس Expense لیست Payments را اضافه کردی
                    .HasForeignKey(d => d.ExpenseId)
                    .OnDelete(DeleteBehavior.Restrict); // جلوگیری از حذف هزینه در صورت وجود پرداخت

                entity.Property(e => e.Amount).HasPrecision(18, 2);
            });

            builder.Entity<Expense>(e =>
            {
                e.ToTable("Expenses");

                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasIndex(x => new { x.TenantId, x.ProjectId, x.ExpenseDate });

                e.HasOne(x => x.BankAccount)
                 .WithMany()
                 .HasForeignKey(x => x.BankAccountId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= FiscalYear =========
            builder.Entity<FiscalYear>(e =>
            {
                e.ToTable("FiscalYears");

                e.HasIndex(x => new { x.TenantId, x.IsCurrent })
                 .HasFilter("[IsCurrent] = 1")
                 .IsUnique();

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= Product =========
            builder.Entity<Product>(e =>
            {
                e.ToTable("Products");
                e.HasIndex(x => new { x.TenantId, x.ProductTitle, x.IsDeleted });
                e.Property(x => x.ProductTitle).HasMaxLength(200);
                e.Property(x => x.ProductUnit).HasMaxLength(50);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= ProductPrice (history of SellPrice) =========
            builder.Entity<ProductPrice>(e =>
            {
                e.ToTable("ProductPrices");

                e.HasOne(x => x.Product)
                 .WithMany(p => p.ProductPrices)
                 .HasForeignKey(x => x.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.SellPrice).HasPrecision(18, 2);

                e.HasIndex(x => new { x.ProductId, x.EffectiveFrom });
                e.HasIndex(x => new { x.ProductId, x.EffectiveTo });

                // فقط یک قیمت فعال به‌ازای هر محصول
                e.HasIndex(x => x.ProductId)
                 .IsUnique()
                 .HasFilter("[EffectiveTo] IS NULL");

                e.ToTable(tb => tb.HasCheckConstraint(
                    "CK_ProductPrice_EffectiveTo",
                    "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]"));

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= PurchaseInvoice =========
            builder.Entity<PurchaseInvoice>(e =>
            {
                e.ToTable("PurchaseInvoices");

                e.HasOne(x => x.Supplier)
                 .WithMany()
                 .HasForeignKey(x => x.SupplierId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.Subtotal).HasPrecision(18, 2);
                e.Property(x => x.Tax).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);

                // شماره فاکتور خرید یکتا در هر Tenant
                e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            });

            // ========= PurchaseInvoiceItem =========
            builder.Entity<PurchaseInvoiceItem>(e =>
            {
                e.ToTable("PurchaseInvoiceItems");

                e.HasOne(d => d.PurchaseInvoice)
                 .WithMany(f => f.Items)
                 .HasForeignKey(d => d.PurchaseInvoiceId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(d => d.Product)
                 .WithMany()
                 .HasForeignKey(d => d.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(d => d.Tenant)
                 .WithMany()
                 .HasForeignKey(d => d.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(d => d.Quantity).HasPrecision(18, 3);
                e.Property(d => d.UnitPricePurchase).HasPrecision(18, 2);
                e.Property(d => d.LineTotal).HasPrecision(18, 2);

                e.ToTable(tb => tb.HasCheckConstraint("CK_PurchaseItem_Qty", "[Quantity] >= 0"));
            });

            // ========= SalesInvoice =========
            builder.Entity<SalesInvoice>(e =>
            {
                e.ToTable("SalesInvoices");

                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.Subtotal).HasPrecision(18, 2);
                e.Property(x => x.Tax).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);

                // شماره فاکتور فروش یکتا در هر Tenant
                e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();
            });
            builder.Entity<SalesInvoice>(entity =>
            {
                entity.Property(x => x.Status)
                      .HasDefaultValue(InvoiceStatus.Proforma)
                      .IsRequired();
            });

            // ========= SalesInvoiceItem =========
            builder.Entity<SalesInvoiceItem>(e =>
            {
                e.ToTable("SalesInvoiceItems");

                e.HasOne(d => d.SalesInvoice)
                 .WithMany(f => f.Items)
                 .HasForeignKey(d => d.SalesInvoiceId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(d => d.Product)
                 .WithMany()
                 .HasForeignKey(d => d.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(d => d.Tenant)
                 .WithMany()
                 .HasForeignKey(d => d.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(d => d.Quantity).HasPrecision(18, 3);
                e.Property(d => d.UnitPriceSell).HasPrecision(18, 2);
                e.Property(d => d.Discount).HasPrecision(18, 2);
                e.Property(d => d.LineTotal).HasPrecision(18, 2);

                e.ToTable(tb => tb.HasCheckConstraint("CK_SalesItem_Qty", "[Quantity] >= 0"));
            });

            // ========= Payment =========
            builder.Entity<Payment>(e =>
            {
                e.ToTable("Payments");

                e.HasKey(x => x.Id);

                e.HasOne(x => x.PurchaseInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.PurchaseInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Expense)
                    .WithMany(x => x.Payments)
                    .HasForeignKey(x => x.ExpenseId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.BankAccount)
                    .WithMany()
                    .HasForeignKey(x => x.BankAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FiscalYear)
                    .WithMany()
                    .HasForeignKey(x => x.FiscalYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                    .WithMany()
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= Receipt =========
            builder.Entity<Receipt>(e =>
            {
                e.ToTable("Receipts");

                e.HasKey(x => x.Id);

                e.HasOne(x => x.SalesInvoice)
                    .WithMany()
                    .HasForeignKey(x => x.SalesInvoiceId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Customer)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.BankAccount)
                    .WithMany()
                    .HasForeignKey(x => x.BankAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FiscalYear)
                    .WithMany()
                    .HasForeignKey(x => x.FiscalYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                    .WithMany()
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= Attachment =========
            builder.Entity<Attachment>(entity =>
            {
                entity.Property(a => a.FileName).HasMaxLength(256);
                entity.Property(a => a.ContentType).HasMaxLength(100);
                entity.Property(a => a.StoragePath).HasMaxLength(500);

                entity.HasOne(a => a.Tenant)
                      .WithMany()
                      .HasForeignKey(a => a.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            builder.Entity<Inventory>()
                .Property(x => x.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            // ========= Inventory =========
            builder.Entity<Inventory>(entity =>
            {
                entity.HasOne(x => x.Warehouse)
                      .WithMany()
                      .HasForeignKey(x => x.WarehouseId)
                      .OnDelete(DeleteBehavior.Restrict); // تغییر از Cascade به Restrict
            });

            // ========= InventoryTransaction =========
            builder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasOne(x => x.Warehouse)
                      .WithMany()
                      .HasForeignKey(x => x.WarehouseId)
                      .OnDelete(DeleteBehavior.Restrict); // تغییر از Cascade به Restrict

                // اگر رابطه‌ای به سال مالی دارد، آن را هم Restrict کن
                entity.HasOne(x => x.FiscalYear)
                      .WithMany()
                      .HasForeignKey(x => x.FiscalYearId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AccountingAccount>(entity =>
            {
                entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
                entity.Property(x => x.Title).HasMaxLength(200).IsRequired();

                // کد حساب در هر شرکت (Tenant) باید یکتا باشد
                entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

                entity.HasOne(x => x.Parent)
                      .WithMany(x => x.Children)
                      .HasForeignKey(x => x.ParentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Tenant)
                      .WithMany()
                      .HasForeignKey(x => x.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<JournalVoucher>(entity =>
            {
                entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
                entity.Property(x => x.TotalDebit).HasPrecision(18, 2);
                entity.Property(x => x.TotalCredit).HasPrecision(18, 2);

                // شماره سند در هر شرکت و هر سال مالی یکتا است
                entity.HasIndex(x => new { x.TenantId, x.FiscalYearId, x.VoucherNumber }).IsUnique();

                entity.HasOne(x => x.FiscalYear)
                      .WithMany()
                      .HasForeignKey(x => x.FiscalYearId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Tenant)
                      .WithMany()
                      .HasForeignKey(x => x.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<JournalVoucherItem>(entity =>
            {
                entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
                entity.Property(x => x.Debit).HasPrecision(18, 2);
                entity.Property(x => x.Credit).HasPrecision(18, 2);

                entity.HasOne(x => x.JournalVoucher)
                      .WithMany(v => v.Items)
                      .HasForeignKey(x => x.JournalVoucherId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Account)
                      .WithMany(a => a.VoucherItems)
                      .HasForeignKey(x => x.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<BankAccount>(entity =>
            {
                entity.ToTable("BankAccounts");
                entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
                entity.Property(x => x.BankName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.AccountNumber).HasMaxLength(50);
                entity.Property(x => x.ShebaNumber).HasMaxLength(35);
                entity.Property(x => x.CardNumber).HasMaxLength(25);
                entity.Property(x => x.InitialBalance).HasPrecision(18, 2);

                entity.HasOne(x => x.Tenant)
                      .WithMany()
                      .HasForeignKey(x => x.TenantId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Cheque>(entity =>
            {
                entity.ToTable("Cheques");
                entity.Property(x => x.ChequeNumber).HasMaxLength(50).IsRequired();
                entity.Property(x => x.SayadiId).HasMaxLength(30);
                entity.Property(x => x.BankName).HasMaxLength(150).IsRequired();
                entity.Property(x => x.BranchName).HasMaxLength(150);
                entity.Property(x => x.DrawerName).HasMaxLength(200);
                entity.Property(x => x.Amount).HasPrecision(18, 2);
                entity.Property(x => x.Description).HasMaxLength(500);

                entity.HasIndex(x => new { x.TenantId, x.Type, x.Status, x.DueDate });

                entity.HasOne(x => x.FiscalYear).WithMany().HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.BankAccount).WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.EndorsedToSupplier).WithMany().HasForeignKey(x => x.EndorsedToSupplierId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            });

            // ========= SalesReturnInvoice =========
            builder.Entity<SalesReturnInvoice>(e =>
            {
                e.ToTable("SalesReturnInvoices");

                e.Property(x => x.Number).IsRequired().HasMaxLength(64);
                e.Property(x => x.Subtotal).HasPrecision(18, 2);
                e.Property(x => x.Tax).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);
                e.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(InvoiceStatus.Proforma);

                e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();

                e.HasOne(x => x.SalesInvoice)
                 .WithMany()
                 .HasForeignKey(x => x.SalesInvoiceId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Project)
                 .WithMany()
                 .HasForeignKey(x => x.ProjectId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Warehouse)
                 .WithMany()
                 .HasForeignKey(x => x.WarehouseId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.SellerUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FiscalYear)
                 .WithMany()
                 .HasForeignKey(x => x.FiscalYearId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= SalesReturnInvoiceItem =========
            builder.Entity<SalesReturnInvoiceItem>(e =>
            {
                e.ToTable("SalesReturnInvoiceItems");

                e.HasOne(d => d.SalesReturnInvoice)
                 .WithMany(f => f.Items)
                 .HasForeignKey(d => d.SalesReturnInvoiceId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(d => d.Product)
                 .WithMany()
                 .HasForeignKey(d => d.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(d => d.Tenant)
                 .WithMany()
                 .HasForeignKey(d => d.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(d => d.Quantity).HasPrecision(18, 3);
                e.Property(d => d.UnitPriceReturn).HasPrecision(18, 2);
                e.Property(d => d.Discount).HasPrecision(18, 2);
                e.Property(d => d.Tax).HasPrecision(18, 2);
                e.Property(d => d.LineTotal).HasPrecision(18, 2);
                e.Property(d => d.UnitCost).HasPrecision(18, 2);
                e.Property(d => d.TotalCost).HasPrecision(18, 2);
            });

            // ========= PurchaseReturnInvoice =========
            builder.Entity<PurchaseReturnInvoice>(e =>
            {
                e.ToTable("PurchaseReturnInvoices");

                e.Property(x => x.Number).IsRequired().HasMaxLength(64);
                e.Property(x => x.Subtotal).HasPrecision(18, 2);
                e.Property(x => x.Tax).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);
                e.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(InvoiceStatus.Proforma);

                e.HasIndex(x => new { x.TenantId, x.Number }).IsUnique();

                e.HasOne(x => x.PurchaseInvoice)
                 .WithMany()
                 .HasForeignKey(x => x.PurchaseInvoiceId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Supplier)
                 .WithMany()
                 .HasForeignKey(x => x.SupplierId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Warehouse)
                 .WithMany()
                 .HasForeignKey(x => x.WarehouseId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.User)
                 .WithMany()
                 .HasForeignKey(x => x.PurchaserUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.FiscalYear)
                 .WithMany()
                 .HasForeignKey(x => x.FiscalYearId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ========= PurchaseReturnInvoiceItem =========
            builder.Entity<PurchaseReturnInvoiceItem>(e =>
            {
                e.ToTable("PurchaseReturnInvoiceItems");

                e.HasOne(d => d.PurchaseReturnInvoice)
                 .WithMany(f => f.Items)
                 .HasForeignKey(d => d.PurchaseReturnInvoiceId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(d => d.Product)
                 .WithMany()
                 .HasForeignKey(d => d.ProductId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(d => d.Tenant)
                 .WithMany()
                 .HasForeignKey(d => d.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(d => d.Quantity).HasPrecision(18, 3);
                e.Property(d => d.UnitPriceReturn).HasPrecision(18, 2);
                e.Property(d => d.Discount).HasPrecision(18, 2);
                e.Property(d => d.Tax).HasPrecision(18, 2);
                e.Property(d => d.LineTotal).HasPrecision(18, 2);
            });
        }
        private void ApplyFilters(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                var parameter = Expression.Parameter(clrType, "e");
                Expression? combinedBody = null;

                // فیلتر Tenant
                if (typeof(TenantScopedEntity).IsAssignableFrom(clrType))
                {
                    var tenantProp = Expression.Property(parameter, nameof(TenantScopedEntity.TenantId));
                    // استفاده از پراپرتی تعریف شده خودمون
                    var currentTenant = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));

                    combinedBody = Expression.Equal(tenantProp, currentTenant);
                }

                // فیلتر FiscalYear
                if (typeof(IFiscalYearScoped).IsAssignableFrom(clrType))
                {
                    var fiscalProp = Expression.Property(parameter, nameof(IFiscalYearScoped.FiscalYearId));
                    var currentFiscal = Expression.Property(Expression.Constant(this), nameof(CurrentFiscalYearId));

                    // مدیریت null بودن FiscalYear
                    var hasValue = Expression.Property(currentFiscal, nameof(Nullable<int>.HasValue));
                    var value = Expression.Property(currentFiscal, nameof(Nullable<int>.Value));

                    var fiscalFilter = Expression.OrElse(
                        Expression.Not(hasValue),
                        Expression.Equal(fiscalProp, value)
                    );

                    combinedBody = combinedBody == null
                        ? fiscalFilter
                        : Expression.AndAlso(combinedBody, fiscalFilter);
                }

                if (combinedBody != null)
                {
                    modelBuilder.Entity(clrType).HasQueryFilter(Expression.Lambda(combinedBody, parameter));
                }
            }
        }
        private static void ConfigureAuditedEntity<TEntity>(ModelBuilder builder) where TEntity : AuditedEntity
        {
            builder.Entity<TEntity>()
                .HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TEntity>()
                .HasOne(e => e.ModifiedBy)
                .WithMany()
                .HasForeignKey(e => e.ModifiedById)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
        private void ApplyAuditTenantAndFiscalYearInfo()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
            {
                // Tenant
                if (entry.Entity is TenantScopedEntity tenantEntity)
                {
                    if (tenantEntity.TenantId == 0 && _currentUserService.TenantId.HasValue)
                        tenantEntity.TenantId = _currentUserService.TenantId.Value;
                }

                // FiscalYear
                if (entry.Entity is IFiscalYearScoped fiscalEntity)
                {
                    if (fiscalEntity.FiscalYearId == 0 && _currentFiscalYearService.FiscalYearId.HasValue)
                        fiscalEntity.FiscalYearId = _currentFiscalYearService.FiscalYearId.Value;
                }

                // Audit fields
                if (entry.Entity is AuditedEntity auditedEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (auditedEntity.CreatedAt == default)
                            auditedEntity.CreatedAt = DateTime.UtcNow;

                        if (string.IsNullOrWhiteSpace(auditedEntity.CreatedById))
                            auditedEntity.CreatedById = _currentUserService.UserId;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditedEntity.ModifiedAt = DateTime.UtcNow;
                        auditedEntity.ModifiedById = _currentUserService.UserId;
                    }
                }
            }
        }
        public override int SaveChanges()
        {
            cleanStr();
            ApplyAuditTenantAndFiscalYearInfo();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            cleanStr();
            ApplyAuditTenantAndFiscalYearInfo();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cleanStr();
            ApplyAuditTenantAndFiscalYearInfo();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            cleanStr();
            ApplyAuditTenantAndFiscalYearInfo();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void cleanStr()
        {
            var changedEntities = ChangeTracker.Entries()
                //پيدا کردن رکوردهایی که عمليات افزودن و ویرایش دارند
                .Where(x => x.State == EntityState.Added || x.State == EntityState.Modified);


            foreach (var item in changedEntities)
            {
                if (item.Entity == null)
                    continue;
                //فيلدهايي که مقدارشون استرينگ باشه و قابليت خواندن و نوشتن داشته باشند
                var properties = item.Entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(string));

                foreach (var property in properties)
                {
                    //اگر پراپرتي مقدار داشت
                    var propName = property.Name;
                    var val = (string)property.GetValue(item.Entity, null);

                    if (val.HasValue())
                    {
                        var newVal = val.Fa2En().FixPersianChars();
                        if (newVal == val)
                            continue;
                        property.SetValue(item.Entity, newVal, null);
                    }
                }
            }
        }
    }
}