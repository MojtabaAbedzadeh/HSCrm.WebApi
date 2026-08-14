using HSCrm.Entities.Entities;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IInventoryService
    {
        void ApplyInventoryForSalesInvoice(SalesInvoice invoice);
        void RollbackInventory(SalesInvoice invoice);
        Task ApplyInventoryForPurchaseInvoice(PurchaseInvoice invoice);
        void RollbackInventoryForPurchaseInvoice(PurchaseInvoice invoice);
    }
}
