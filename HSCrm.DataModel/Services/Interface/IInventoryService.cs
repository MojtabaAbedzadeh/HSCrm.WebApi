using HSCrm.Entities.Entities;
using HSCrm.Entities.Models.Dto;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IInventoryService
    {
        void ApplyInventoryForSalesInvoice(SalesInvoice invoice);
        void RollbackInventory(SalesInvoice invoice);
        Task ApplyInventoryForPurchaseInvoice(PurchaseInvoice invoice);
        void RollbackInventoryForPurchaseInvoice(PurchaseInvoice invoice);
        Task<List<KardexDto>> GetProductKardexAsync(int productId, DateTime? fromDate, DateTime? toDate, int? warehouseId = null);
        Task ApplyAdjustmentAsync(AdjustmentDto dto, CancellationToken ct = default);
    }
}
