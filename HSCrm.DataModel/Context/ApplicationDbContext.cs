using HSCrm.Common.PublicTools;
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
        private int CurrentTenantId => _currentUserService?.TenantId ?? 0;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService currentUserService) : base(options)
        {
            _currentUserService = currentUserService;
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
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
        public DbSet<FiscalYear> FiscalYears { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                if (typeof(TenantScopedEntity).IsAssignableFrom(entityType.ClrType))
                {
                    builder.Entity(entityType.ClrType)
                        .HasQueryFilter(GetTenantFilterExpression(entityType.ClrType));
                }
            }

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
                    UserType = 1,
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
                      .HasDefaultValue(SalesInvoiceStatus.Proforma)
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

                e.HasOne(x => x.SalesInvoice)
                 .WithMany()
                 .HasForeignKey(x => x.SalesInvoiceId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.PurchaseInvoice)
                 .WithMany()
                 .HasForeignKey(x => x.PurchaseInvoiceId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.Amount).HasPrecision(18, 2);
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
        private void ApplyAuditAndTenantInfo()
        {
            var userId = _currentUserService?.UserId;
            var tenantId = _currentUserService?.TenantId;
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries())
            {
                // -------------------------
                // Audit Handling
                // -------------------------
                if (entry.Entity is AuditedEntity auditedEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditedEntity.CreatedAt = now;
                        auditedEntity.CreatedById = userId;
                        auditedEntity.IsDeleted = false;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditedEntity.ModifiedAt = now;
                        auditedEntity.ModifiedById = userId;
                    }
                }

                // -------------------------
                // Tenant Handling
                // -------------------------
                if (entry.Entity is TenantScopedEntity tenantEntity)
                {
                    if (entry.State == EntityState.Added)
                    {
                        // اگر کاربر tenant داشت
                        if (tenantId.HasValue)
                        {
                            tenantEntity.TenantId = tenantId.Value;
                        }
                        // اگر سیستم در حال seed یا migration بود
                        else if (tenantEntity.TenantId == 0)
                        {
                            tenantEntity.TenantId = 1;
                        }
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        // جلوگیری از تغییر TenantId
                        entry.Property(nameof(TenantScopedEntity.TenantId)).IsModified = false;
                    }
                }
            }
        }
        public override int SaveChanges()
        {
            cleanStr();

            ApplyAuditAndTenantInfo();
            return base.SaveChanges();
        }
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            cleanStr();
            ApplyAuditAndTenantInfo();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            cleanStr();
            ApplyAuditAndTenantInfo();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cleanStr();
            ApplyAuditAndTenantInfo();

            return base.SaveChangesAsync(cancellationToken);
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

        private LambdaExpression GetTenantFilterExpression(Type entityType)
        {
            var parameter = Expression.Parameter(entityType, "e");

            var tenantProperty = Expression.Property(parameter, nameof(TenantScopedEntity.TenantId));

            var currentTenant = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId)
            );

            var body = Expression.Equal(tenantProperty, currentTenant);

            return Expression.Lambda(body, parameter);
        }

    }
}