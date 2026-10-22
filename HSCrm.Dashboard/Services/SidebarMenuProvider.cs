using HSCrm.Dashboard.Costants;
using HSCrm.Dashboard.Models;
using HSCrm.Dashboard.Services.Interface;
using System.Security.Claims;

namespace HSCrm.Dashboard.Services
{
    public class SidebarMenuProvider : ISidebarMenuProvider
    {
        public List<SidebarMenuItem> GetMenuForUser(ClaimsPrincipal user)
        {
            var allMenus = BuildMenus();
            return FilterMenusByPermission(allMenus, user);
        }

        private List<SidebarMenuItem> FilterMenusByPermission(
            List<SidebarMenuItem> menus,
            ClaimsPrincipal user)
        {
            var filtered = new List<SidebarMenuItem>();

            foreach (var menu in menus)
            {
                bool hasPermission = string.IsNullOrEmpty(menu.PermissionKey)
                                     || user.HasClaim("Permission", menu.PermissionKey);

                if (menu.Children != null && menu.Children.Any())
                {
                    var visibleChildren = FilterMenusByPermission(menu.Children, user);
                    if (visibleChildren.Any())
                    {
                        menu.Children = visibleChildren;
                        filtered.Add(menu);
                        continue;
                    }
                }

                if (hasPermission)
                    filtered.Add(menu);
            }

            return filtered;
        }

        public List<SidebarMenuItem> BuildMenus()
        {
            return new List<SidebarMenuItem>
            {
                new SidebarMenuItem
                {
                    Key = "dashboard",
                    Title = "داشبورد",
                    Icon = "mdi mdi-view-dashboard",
                    Url = "/AdminArea/Home/Index",
                },
                new SidebarMenuItem
                {
                    Key = "customers",
                    Title = "مشتریان",
                    Icon = "mdi mdi-account-group",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "customers_list",
                            Title = "لیست مشتری‌ها",
                            Url = "/AdminArea/Customer/Index",
                            PermissionKey = Permissions.Customers.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "suppliers",
                    Title = "تأمین‌کنندگان",
                    Icon = "mdi mdi-truck",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "suppliers_list",
                            Title = "لیست تامین‌کنندگان",
                            Url = "/AdminArea/Supplier/Index",
                            PermissionKey = Permissions.Suppliers.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "users",
                    Title = "کاربران",
                    Icon = "mdi mdi-account",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "users_list",
                            Title = "لیست کاربران",
                            Url = "/AdminArea/User/Index",
                            PermissionKey = Permissions.Users.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "roles_list",
                            Title = "نقش‌ها",
                            Url = "/AdminArea/Role/Index",
                            PermissionKey = Permissions.Roles.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "products",
                    Title = "محصولات",
                    Icon = "mdi mdi-cube-outline",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "products_list",
                            Title = "لیست محصولات",
                            Url = "/AdminArea/Product/Index",
                            PermissionKey = Permissions.Products.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "product_kardex",
                            Title = "کاردکس محصول",
                            Url = "/AdminArea/Inventory/KardexReport",
                            PermissionKey = Permissions.Products.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "product_prices",
                            Title = "قیمت‌ها",
                            Url = "/AdminArea/ProductPrice/Index",
                            PermissionKey = Permissions.ProductPrices.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "inventories",
                    Title = "انبارها",
                    Icon = "mdi mdi-warehouse",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "warehouses_list",
                            Title = "لیست انبارها",
                            Url = "/AdminArea/Warehouse/Index",
                            PermissionKey = Permissions.Inventories.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "projects",
                    Title = "پروژه‌ها",
                    Icon = "mdi mdi-folder-outline",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "projects_list",
                            Title = "لیست پروژه‌ها",
                            Url = "/AdminArea/Project/Index",
                            PermissionKey = Permissions.Projects.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "invoices",
                    Title = "فاکتورها و برگشتی‌ها",
                    Icon = "mdi mdi-receipt",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "sales_invoices",
                            Title = "فاکتورهای فروش",
                            Url = "/AdminArea/Invoice/SalesInvoice",
                            PermissionKey = Permissions.SalesInvoices.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "purchase_invoices",
                            Title = "فاکتورهای خرید",
                            Url = "/AdminArea/Invoice/PurchaseInvoice",
                            PermissionKey = Permissions.PurchaseInvoices.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "sales_return_invoices",
                            Title = "برگشت از فروش (فاز ۵)",
                            Url = "/AdminArea/ReturnInvoice/SalesReturn",
                            PermissionKey = Permissions.SalesInvoices.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "purchase_return_invoices",
                            Title = "برگشت از خرید (فاز ۵)",
                            Url = "/AdminArea/ReturnInvoice/PurchaseReturn",
                            PermissionKey = Permissions.PurchaseInvoices.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "treasury",
                    Title = "خزانه‌داری، بانک و چک",
                    Icon = "mdi mdi-bank",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "bank_accounts",
                            Title = "حساب‌های بانکی و صندوق",
                            Url = "/AdminArea/BankAccount/Index",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "receipts_list",
                            Title = "دریافت‌ها و واریزی‌ها",
                            Url = "/AdminArea/Receipt/Index",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "payments",
                            Title = "پرداخت‌ها",
                            Url = "/AdminArea/Payment/Index",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "cheques_list",
                            Title = "چک‌های صیادی و رأس‌گیری",
                            Url = "/AdminArea/Cheque/Index",
                            PermissionKey = Permissions.Payments.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "accounting",
                    Title = "حسابداری و گزارشات مالی",
                    Icon = "mdi mdi-calculator-variant",
                    Children = new List<SidebarMenuItem>
                    {
                        new SidebarMenuItem
                        {
                            Key = "accounting_accounts",
                            Title = "درختواره و کدینگ حساب‌ها",
                            Url = "/AdminArea/Accounting/Accounts",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "accounting_vouchers",
                            Title = "اسناد حسابداری (دفتر روزنامه)",
                            Url = "/AdminArea/Accounting/Vouchers",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "accounting_reports",
                            Title = "تراز آزمایشی، معین و سود/زیان",
                            Url = "/AdminArea/Accounting/Reports",
                            PermissionKey = Permissions.Payments.View
                        },
                        new SidebarMenuItem
                        {
                            Key = "accounting_aging",
                            Title = "صورت‌حساب ۳۶۰ و تحلیل سنی",
                            Url = "/AdminArea/Accounting/AgingAndStatements",
                            PermissionKey = Permissions.Payments.View
                        }
                    }
                },
                new SidebarMenuItem
                {
                    Key = "expenses",
                    Title = "هزینه‌ها",
                    Icon = "mdi mdi-cash",
                    Url = "/AdminArea/Expense/Index",
                    PermissionKey = Permissions.Expenses.View
                },
                new SidebarMenuItem
                {
                    Key = "fiscal_years",
                    Title = "سال مالی و بستن حساب‌ها",
                    Icon = "mdi mdi-calendar-check",
                    Url = "/AdminArea/FiscalYear/Index",
                    PermissionKey = Permissions.FiscalYears.View
                }
            };
        }
    }
}
