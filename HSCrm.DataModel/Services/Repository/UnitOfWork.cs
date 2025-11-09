using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Transactions;

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
        private GenericCRUDClass<Tenant>? _business;
        private GenericCRUDClass<PurchaseInvoice>? _factor;
        private GenericCRUDClass<PurchaseInvoiceItem>? _factorDetail;
        private GenericCRUDClass<ProductPrice>? _price;
        private GenericCRUDClass<Product>? _product;
        private GenericCRUDClass<Project>? _project;
        private GenericCRUDClass<Supplier>? _supplier;

        public GenericCRUDClass<ApplicationUser> userManagerUW => _userManager ??= new GenericCRUDClass<ApplicationUser>(_context);
        public GenericCRUDClass<ApplicationRole> roleManagerUW => _roleManager ??= new GenericCRUDClass<ApplicationRole>(_context);
        public GenericCRUDClass<Tenant> businessUW => _business ??= new GenericCRUDClass<Tenant>(_context);
        public GenericCRUDClass<PurchaseInvoice> factorUW => _factor ??= new GenericCRUDClass<PurchaseInvoice>(_context);
        public GenericCRUDClass<PurchaseInvoiceItem> factorDetailUW => _factorDetail ??= new GenericCRUDClass<PurchaseInvoiceItem>(_context);
        public GenericCRUDClass<ProductPrice> priceUW => _price ??= new GenericCRUDClass<ProductPrice>(_context);
        public GenericCRUDClass<Product> productUW => _product ??= new GenericCRUDClass<Product>(_context);
        public GenericCRUDClass<Project> projectUW => _project ??= new GenericCRUDClass<Project>(_context);
        public GenericCRUDClass<Supplier> supplierUW => _supplier ??= new GenericCRUDClass<Supplier>(_context);


        public IEntityTransaction BeginTransaction()
            => new EntityTransaction(_context); 
        public async Task<IEntityTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => await EntityTransaction.StartAsync(_context, ct);

        public int Save()
        {
            using var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            var total = _context.SaveChanges();
            scope.Complete();
            return total;
        }

        public async Task<int> SaveAsync(CancellationToken ct = default)
        {
            using var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
            var total = await _context.SaveChangesAsync(ct);
            scope.Complete();
            return total;
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
