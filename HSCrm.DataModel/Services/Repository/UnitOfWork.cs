using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;

namespace HSCrm.DataModel.Services.Repository
{
    public class UnitOfWork : IDisposable, IUnitOfWork
    {
        private readonly ApplicationDbContext _context;


        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        private GenericCRUDClass<ApplicationUser>? _userManager;
        private GenericCRUDClass<ApplicationRole>? _roleManager;
        private GenericCRUDClass<Tenant>? _tenant;
        private GenericCRUDClass<PurchaseInvoice>? _factor;
        private GenericCRUDClass<PurchaseInvoiceItem>? _factorDetail;
        private GenericCRUDClass<ProductPrice>? _price;
        private GenericCRUDClass<Product>? _product;
        private GenericCRUDClass<Project>? _project;
        private GenericCRUDClass<Supplier>? _supplier;
        private GenericCRUDClass<Customer>? _customer;
        private GenericCRUDClass<Expense>? _expense;
        private GenericCRUDClass<Payment>? _payment;
        private GenericCRUDClass<Receipt>? _receipt;
        private GenericCRUDClass<SalesInvoice>? _salesInvoice;
        private GenericCRUDClass<SalesInvoiceItem>? _salesInvoiceItem;
        private GenericCRUDClass<Inventory>? _inventory;
        private GenericCRUDClass<InventoryTransaction>? _inventoryTransaction;
        private GenericCRUDClass<FiscalYear>? _fiscalYear;
        private GenericCRUDClass<Warehouse>? _warehouse;
        private GenericCRUDClass<UserRole>? _userRole;
        private GenericCRUDClass<Permission>? _permission;
        private GenericCRUDClass<RolePermission>? _rolePermission;
        private GenericCRUDClass<AccountingAccount>? _accountingAccount;
        private GenericCRUDClass<JournalVoucher>? _journalVoucher;
        private GenericCRUDClass<JournalVoucherItem>? _journalVoucherItem;

        public GenericCRUDClass<ApplicationUser> userManagerUW => _userManager ??= new GenericCRUDClass<ApplicationUser>(_context);
        public GenericCRUDClass<ApplicationRole> roleManagerUW => _roleManager ??= new GenericCRUDClass<ApplicationRole>(_context);
        public GenericCRUDClass<Tenant> tenantUW => _tenant ??= new GenericCRUDClass<Tenant>(_context);
        public GenericCRUDClass<PurchaseInvoice> purchaseInvoiceUW => _factor ??= new GenericCRUDClass<PurchaseInvoice>(_context);
        public GenericCRUDClass<PurchaseInvoiceItem> purchaseInvoiceDetailUW => _factorDetail ??= new GenericCRUDClass<PurchaseInvoiceItem>(_context);
        public GenericCRUDClass<ProductPrice> priceUW => _price ??= new GenericCRUDClass<ProductPrice>(_context);
        public GenericCRUDClass<Product> productUW => _product ??= new GenericCRUDClass<Product>(_context);
        public GenericCRUDClass<Project> projectUW => _project ??= new GenericCRUDClass<Project>(_context);
        public GenericCRUDClass<Supplier> supplierUW => _supplier ??= new GenericCRUDClass<Supplier>(_context);
        public GenericCRUDClass<Customer> customerUW => _customer ??= new GenericCRUDClass<Customer>(_context);
        public GenericCRUDClass<Expense> expenseUW => _expense ??= new GenericCRUDClass<Expense>(_context);
        public GenericCRUDClass<Payment> paymentUW => _payment ??= new GenericCRUDClass<Payment>(_context);
        public GenericCRUDClass<Receipt> receiptUW => _receipt ??= new GenericCRUDClass<Receipt>(_context);
        public GenericCRUDClass<SalesInvoice> salesInvoiceUW => _salesInvoice ??= new GenericCRUDClass<SalesInvoice>(_context);
        public GenericCRUDClass<SalesInvoiceItem> salesInvoiceItemUW => _salesInvoiceItem ??= new GenericCRUDClass<SalesInvoiceItem>(_context);
        public GenericCRUDClass<Inventory> inventoryUW => _inventory ??= new GenericCRUDClass<Inventory>(_context);
        public GenericCRUDClass<InventoryTransaction> inventoryTransactionUW => _inventoryTransaction ??= new GenericCRUDClass<InventoryTransaction>(_context);
        public GenericCRUDClass<FiscalYear> fiscalYearUW => _fiscalYear ??= new GenericCRUDClass<FiscalYear>(_context);
        public GenericCRUDClass<Warehouse> warehouseUW => _warehouse ??= new GenericCRUDClass<Warehouse>(_context);
        public GenericCRUDClass<UserRole> userRoleUW => _userRole??= new GenericCRUDClass<UserRole>(_context);
        public GenericCRUDClass<Permission> permissionUW => _permission ??= new GenericCRUDClass<Permission>(_context);
        public GenericCRUDClass<RolePermission> rolePermissionUW => _rolePermission ??= new GenericCRUDClass<RolePermission>(_context);
        public GenericCRUDClass<AccountingAccount> accountingAccountUW => _accountingAccount ??= new GenericCRUDClass<AccountingAccount>(_context);
        public GenericCRUDClass<JournalVoucher> journalVoucherUW => _journalVoucher ??= new GenericCRUDClass<JournalVoucher>(_context);
        public GenericCRUDClass<JournalVoucherItem> journalVoucherItemUW => _journalVoucherItem ??= new GenericCRUDClass<JournalVoucherItem>(_context);


        public IEntityTransaction BeginTransaction()
        {
            return new EntityTransaction(_context);
        }
        public async Task<IEntityTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            return await EntityTransaction.StartAsync(_context, ct);
        }
        public void Save()
        {
            _context.SaveChanges();
        }
        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }
        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
