using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IUnitOfWork : IDisposable
    {
        GenericCRUDClass<ApplicationUser> userManagerUW { get; }
        GenericCRUDClass<ApplicationRole> roleManagerUW { get; }
        GenericCRUDClass<Tenant> tenantUW { get; }
        GenericCRUDClass<PurchaseInvoice> purchaseInvoiceUW { get; }
        GenericCRUDClass<PurchaseInvoiceItem> purchaseInvoiceDetailUW { get; }
        GenericCRUDClass<ProductPrice> priceUW { get; }
        GenericCRUDClass<Product> productUW { get; }
        GenericCRUDClass<Project> projectUW { get; }
        GenericCRUDClass<Supplier> supplierUW { get; }
        GenericCRUDClass<Customer> customerUW { get; }
        GenericCRUDClass<Expense> expenseUW { get; }
        GenericCRUDClass<Receipt> receiptUW { get; }
        GenericCRUDClass<Payment> paymentUW { get; }
        GenericCRUDClass<SalesInvoice> salesInvoiceUW { get; }
        GenericCRUDClass<SalesInvoiceItem> salesInvoiceItemUW { get; }
        GenericCRUDClass<Inventory> inventoryUW { get; }
        GenericCRUDClass<InventoryTransaction> inventoryTransactionUW { get; }
        GenericCRUDClass<Warehouse> warehouseUW { get; }
        GenericCRUDClass<FiscalYear> fiscalYearUW { get; }
        GenericCRUDClass<UserRole> userRoleUW{ get; }
        GenericCRUDClass<Permission> permissionUW{ get; }
        GenericCRUDClass<RolePermission> rolePermissionUW{ get; }

        IEntityTransaction BeginTransaction();
        Task<IEntityTransaction> BeginTransactionAsync(CancellationToken ct = default);
        void Save();
        Task SaveAsync();
    }
}
