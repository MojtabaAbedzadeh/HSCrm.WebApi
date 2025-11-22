using HSCrm.Common.PublicTools;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;


namespace HSCrm.DataModel.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // === DbSets ===
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ========= Seed Roles & Admin User =========
            var adminRoleId = "AdminId";
            var userRoleId = "UserId";
            var userId = "28dfc395-0464-4b47-8aae-c73953a9d153";

            builder.Entity<ApplicationRole>().HasData(
                new ApplicationRole { Id = adminRoleId, Name = "Admin", NormalizedName = "ADMIN", ConcurrencyStamp = "cs_admin" },
                new ApplicationRole { Id = userRoleId, Name = "User", NormalizedName = "USER", ConcurrencyStamp = "cs_user" }
            );

            builder.Entity<ApplicationUser>().HasData(
                new ApplicationUser
                {
                    Id = userId,
                    UserFirstName = "admin",
                    UserLastName = "admin",
                    UserImageUrl = "https://localhost:7096/Upload/UserImage/defaultAvatar.png",
                    UserName = "Admin",
                    NormalizedUserName = "ADMIN",
                    PhoneNumber = "09xxxxxxxxx",
                    Email = "admin@example.com",
                    NormalizedEmail = "ADMIN@EXAMPLE.COM",
                    EmailConfirmed = true,
                    UserStatus = true,
                    UserType = 1,
                    PasswordHash = "AQAAAAIAAYagAAAAEAsJ0rn1us3z/MyoCt4F9J63B6xi75XqeTzR+3b+aZPoajU3nTUfPjVQYV+ELmpG5g==",
                    SecurityStamp = "sdcnjrefhsrdv1454rsdv5",
                    ConcurrencyStamp = "sdcnjrefhsrdv1454rsdv5",
                }
            );

            builder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string> { UserId = userId, RoleId = adminRoleId }
            );

            builder.Entity<ApplicationUser>(e =>
            {
                e.ToTable("Users");
                e.Property(x => x.Id).HasColumnName("UserId");
            });
            builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));

            // ========= Global decimal precision (18,2) =========
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

            // ========= Tenant =========
            builder.Entity<Tenant>(e =>
            {
                e.ToTable("Tenants");
                e.HasIndex(x => x.TenantTitle); // جستجوی سریع روی عنوان
                e.Property(x => x.Currency).HasMaxLength(10);
                // Tenant خودش TenantId ندارد؛ پس هیچ FK به خودش تنظیم نمی‌کنیم.
            });

            // ========= Customer =========
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

            // ========= Supplier =========
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

            // ========= Project =========
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

                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.Restrict);

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

            // ========= Expense =========
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
            builder.Entity<Attachment>(e =>
            {
                e.ToTable("Attachments");

                e.HasOne(x => x.Tenant)
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(x => x.FileName).HasMaxLength(260);
                e.Property(x => x.ContentType).HasMaxLength(100);
                e.Property(x => x.StoragePath).HasMaxLength(1000);
                e.HasIndex(x => new { x.TenantId, x.EntityName, x.EntityId });
            });
        }

        public override int SaveChanges()
        {
            cleanStr();

            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            cleanStr();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            cleanStr();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cleanStr();

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
    }
}
