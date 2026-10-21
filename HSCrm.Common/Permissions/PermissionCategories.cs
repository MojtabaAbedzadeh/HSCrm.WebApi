namespace HSCrm.Common.Permissions
{
    public static class PermissionCategories
    {
        public static readonly List<PermissionCategory> All = new()
        {
            new() { Key="Users", TitleFa="کاربران", Icon="mdi mdi-account", Order=1 },
            new() { Key="Roles", TitleFa="نقش‌ها", Icon="mdi mdi-shield-account", Order=2 },
            new() { Key="Customers", TitleFa="مشتریان", Icon="mdi mdi-account-group", Order=3 },
            new() { Key="Suppliers", TitleFa="تأمین‌کنندگان", Icon="mdi mdi-truck", Order=4 },
            new() { Key="Products", TitleFa="محصولات", Icon="mdi mdi-cube-outline", Order=5 },
            new() { Key="ProductPrices", TitleFa="قیمت محصولات", Icon="mdi mdi-cash", Order=6 },
            new() { Key="Warehouses", TitleFa="انبارها", Icon="mdi mdi-warehouse", Order=7 },
            new() { Key="Inventories", TitleFa="موجودی‌ها", Icon="mdi mdi-database", Order=8 },
            new() { Key="Projects", TitleFa="پروژه‌ها", Icon="mdi mdi-briefcase", Order=9 },
            new() { Key="Expenses", TitleFa="هزینه‌ها", Icon="mdi mdi-cash-minus", Order=10 },
            new() { Key="Payments", TitleFa="پرداخت‌ها", Icon="mdi mdi-cash-check", Order=11 },
            new() { Key="PurchaseInvoices", TitleFa="فاکتورهای خرید", Icon="mdi mdi-file-document", Order=12 },
            new() { Key="SalesInvoices", TitleFa="فاکتورهای فروش", Icon="mdi mdi-file-document-outline", Order=13 },
            new() { Key="FiscalYears", TitleFa="سال‌های مالی", Icon="mdi mdi-calendar", Order=14 },
            new() { Key="Tenants", TitleFa="شرکت‌ها", Icon="mdi mdi-domain", Order=15 },
            new() { Key="AccountingAccounts", TitleFa="کدینگ حسابداری", Icon="mdi mdi-domain", Order=16 },
            new() { Key="JournalVouchers", TitleFa="اسناد حسابداری", Icon="mdi mdi-domain", Order=17 },
            new() { Key="AccountingReports", TitleFa="گزارشات مالی و دفاتر", Icon="mdi mdi-domain", Order=18 },
            new() { Key="BankAccounts", TitleFa="حساب‌های بانکی و خزانه", Icon="mdi mdi-bank", Order=19 },
            new() { Key="Cheques", TitleFa="چک‌های دریافتنی و پرداختنی", Icon="mdi mdi-checkbook", Order=20 }
        };

        public static PermissionCategory Get(string key)
        {
            return All.FirstOrDefault(x => x.Key == key)
                   ?? new PermissionCategory
                   {
                       Key = key,
                       TitleFa = key,
                       Icon = "mdi mdi-circle",
                       Order = 999
                   };
        }
    }
}