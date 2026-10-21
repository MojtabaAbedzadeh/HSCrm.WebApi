using HSCrm.Entities.Entities;
using HSCrm.Entities.Models.Dto;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Interface
{
    public interface IInventoryService
    {
        Task ApplyPurchaseInvoice(PurchaseInvoice invoice);
        void ApplyInventoryForSalesInvoice(SalesInvoice invoice, InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage);
        void RollbackInventory(SalesInvoice invoice);
        Task ApplyInventoryForPurchaseInvoice(PurchaseInvoice invoice);
        void RollbackInventoryForPurchaseInvoice(PurchaseInvoice invoice);
        Task ApplyInventoryForSalesReturnAsync(SalesReturnInvoice returnInvoice, CancellationToken ct = default);
        void RollbackInventoryForSalesReturn(SalesReturnInvoice returnInvoice);
        Task ApplyInventoryForPurchaseReturnAsync(PurchaseReturnInvoice returnInvoice, CancellationToken ct = default);
        void RollbackInventoryForPurchaseReturn(PurchaseReturnInvoice returnInvoice);
        Task<List<KardexDto>> GetProductKardexAsync(int productId, DateTime? fromDate, DateTime? toDate, int? warehouseId = null);
        Task<List<InventoryValuationDto>> GetInventoryValuationAsync(int? warehouseId = null, CancellationToken ct = default);
        Task ApplyAdjustmentAsync(AdjustmentDto dto, CancellationToken ct = default);
    }
}
