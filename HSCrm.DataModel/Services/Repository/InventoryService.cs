using HSCrm.DataModel.Context;
using HSCrm.DataModel.Exceptions;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class InventoryService : UnitOfWork, IInventoryService
    {
        public InventoryService(ApplicationDbContext context) : base(context)
        {
        }

        public void ApplyInventoryForSalesInvoice(SalesInvoice invoice)
        {
            foreach (var item in invoice.Items)
            {
                var stock = this.inventoryUW
                    .Get(x => x.ProductId == item.ProductId &&
                              x.TenantId == invoice.TenantId &&
                              x.WarehouseId == invoice.WarehouseId)
                    .SingleOrDefault();

                if (stock == null || stock.Quantity < item.Quantity)
                    throw new NotEnoughInventoryException("موجودی کالا کافی نیست");

                // ثبت گردش
                this.inventoryTransactionUW.Create(new InventoryTransaction
                {
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    FiscalYearId = 1,
                    WarehouseId = invoice.WarehouseId,
                    QuantityChange = -item.Quantity,
                    TransactionType = InventoryTransactionType.Sale,
                    ReferenceId = item.Id,
                    ReferenceType = InventoryReferenceType.SalesInvoiceItem,
                    CreatedById = invoice.CreatedById,
                    CreatedAt = DateTime.UtcNow
                });

                // کاهش موجودی
                stock.Quantity -= item.Quantity;
                this.inventoryUW.Update(stock);
            }
        }
        public void RollbackInventory(SalesInvoice invoice)
        {
            foreach (var item in invoice.Items)
            {
                var stock = this.inventoryUW
                    .Get(x => x.ProductId == item.ProductId &&
                              x.TenantId == invoice.TenantId)
                    .SingleOrDefault();

                if (stock == null)
                    throw new Exception("رکورد موجودی برای کالا یافت نشد");

                // ✅ برگرداندن موجودی
                stock.Quantity += item.Quantity;
                this.inventoryUW.Update(stock);

                // ✅ ثبت گردش برگشتی
                this.inventoryTransactionUW.Create(new InventoryTransaction
                {
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    QuantityChange = item.Quantity, // مثبت
                    TransactionType = InventoryTransactionType.SaleRollback,
                    CreatedById = invoice.CreatedById,
                    ReferenceId = item.Id,
                    ReferenceType = InventoryReferenceType.SalesInvoiceItem,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        public void ApplyInventoryForPurchaseInvoice(PurchaseInvoice invoice)
        {
            foreach (var item in invoice.Items)
            {
                var stock = this.inventoryUW
                    .Get(x => x.ProductId == item.ProductId &&
                              x.TenantId == invoice.TenantId
                               && x.WarehouseId == invoice.WarehouseId)
                    .SingleOrDefault();

                if (stock == null)
                {
                    // ✅ موجودی جدید
                    stock = new Inventory
                    {
                        TenantId = invoice.TenantId,
                        ProductId = item.ProductId,
                        CreatedAt = DateTime.UtcNow,
                        CreatedById = invoice.CreatedById,
                        WarehouseId = invoice.WarehouseId,                        
                        Quantity = item.Quantity // ✅ مستقیم مقدار بده
                    };

                    this.inventoryUW.Create(stock); // ✅ فقط Create
                }
                else
                {
                    // ✅ موجودی قبلی
                    stock.Quantity += item.Quantity;
                    this.inventoryUW.Update(stock); // ✅ فقط برای موجود
                }

                // ✅ ثبت گردش موجودی
                this.inventoryTransactionUW.Create(new InventoryTransaction
                {
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedById = invoice.CreatedById,
                    QuantityChange = item.Quantity,
                    FiscalYearId = 1,
                    WarehouseId = invoice.WarehouseId,
                    TransactionType = InventoryTransactionType.Purchase,
                    ReferenceId = item.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoiceItem,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        public void RollbackInventoryForPurchaseInvoice(PurchaseInvoice invoice)
        {
            foreach (var item in invoice.Items)
            {
                var stock = this.inventoryUW
                    .Get(x => x.ProductId == item.ProductId &&
                              x.TenantId == invoice.TenantId)
                    .SingleOrDefault();

                if (stock == null || stock.Quantity < item.Quantity)
                    throw new Exception("امکان برگشت موجودی وجود ندارد");

                // ✅ کاهش موجودی
                stock.Quantity -= item.Quantity;
                this.inventoryUW.Update(stock);

                // ✅ ثبت گردش برگشتی
                this.inventoryTransactionUW.Create(new InventoryTransaction
                {
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedById = invoice.CreatedById,
                    QuantityChange = -item.Quantity, // منفی
                    TransactionType = InventoryTransactionType.PurchaseRollback,
                    ReferenceId = item.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoiceItem,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
    }
}
