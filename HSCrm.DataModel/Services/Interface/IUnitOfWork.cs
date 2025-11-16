using HSCrm.DataModel.Services.Repository;
using HSCrm.Entities.Entities;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IUnitOfWork
    {
        GenericCRUDClass<ApplicationUser> userManagerUW { get; }
        GenericCRUDClass<ApplicationRole> roleManagerUW { get; }
        GenericCRUDClass<Tenant> businessUW { get; }
        GenericCRUDClass<PurchaseInvoice> factorUW { get; }
        GenericCRUDClass<PurchaseInvoiceItem> factorDetailUW { get; }
        GenericCRUDClass<ProductPrice> priceUW { get; }
        GenericCRUDClass<Product> productUW { get; }
        GenericCRUDClass<Project> projectUW { get; }
        GenericCRUDClass<Supplier> supplierUW { get; }

        IEntityTransaction BeginTransaction();
        Task<IEntityTransaction> BeginTransactionAsync(CancellationToken ct = default);
        int Save();
        Task<int> SaveAsync(CancellationToken ct = default);
    }
}
