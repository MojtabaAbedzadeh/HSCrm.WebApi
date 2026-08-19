using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models.Dto;
using Microsoft.EntityFrameworkCore;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class InventoryService : UnitOfWork, IInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentFiscalYearService _currentFiscalYear;
        private readonly ICurrentUserService _currentUser;

        public InventoryService(ApplicationDbContext context, IUnitOfWork unitOfWork, ICurrentFiscalYearService currentFiscalYear, ICurrentUserService currentUser) : base(context)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _currentFiscalYear = currentFiscalYear;
            _currentUser = currentUser;
        }

        public void ApplyInventoryForSalesInvoice(SalesInvoice invoice)
        {
            if (invoice == null || invoice.Items == null || !invoice.Items.Any())
                return;

            if (invoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in invoice.Items)
            {
                var inventory = _context.Inventories.FirstOrDefault(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == invoice.TenantId &&
                    x.WarehouseId == invoice.WarehouseId);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = item.ProductId,
                        TenantId = invoice.TenantId,
                        WarehouseId = invoice.WarehouseId,
                        CreatedAt = DateTime.UtcNow,
                        Quantity = 0
                    };
                    _context.Inventories.Add(inventory);
                }

                if (inventory.Quantity < item.Quantity)
                    throw new InvalidOperationException("موجودی انبار برای ثبت فروش کافی نیست.");

                inventory.Quantity -= item.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    QuantityChange = -item.Quantity,
                    CreatedAt = DateTime.UtcNow,
                    TransactionType = InventoryTransactionType.Sale,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.SalesInvoice
                });
            }
        }

        public void RollbackInventory(SalesInvoice invoice)
        {
            if (invoice == null || invoice.Items == null || !invoice.Items.Any())
                return;

            if (invoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in invoice.Items)
            {
                var inventory = _context.Inventories.FirstOrDefault(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == invoice.TenantId &&
                    x.WarehouseId == invoice.WarehouseId);

                if (inventory == null)
                    throw new InvalidOperationException("موجودی انبار برای بازگشت فروش پیدا نشد.");

                inventory.Quantity += item.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = item.Quantity,
                    TransactionType = InventoryTransactionType.SaleRollback,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.SalesInvoice
                });
            }
        }

        public async Task ApplyInventoryForPurchaseInvoice(PurchaseInvoice invoice)
        {
            if (invoice == null || invoice.Items == null || !invoice.Items.Any())
                return;

            if (invoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in invoice.Items)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == invoice.TenantId &&
                    x.WarehouseId == invoice.WarehouseId);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = item.ProductId,
                        CreatedAt = DateTime.UtcNow,
                        TenantId = invoice.TenantId,
                        WarehouseId = invoice.WarehouseId,
                        Quantity = 0
                    };
                    await _context.Inventories.AddAsync(inventory);
                }

                inventory.Quantity += item.Quantity;

                await _context.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    CreatedAt = DateTime.UtcNow,
                    ProductId = item.ProductId,
                    QuantityChange = item.Quantity,
                    TransactionType = InventoryTransactionType.Purchase,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoice
                });
            }
        }

        public void RollbackInventoryForPurchaseInvoice(PurchaseInvoice invoice)
        {
            if (invoice == null || invoice.Items == null || !invoice.Items.Any())
                return;

            if (invoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in invoice.Items)
            {
                var inventory = _context.Inventories.FirstOrDefault(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == invoice.TenantId &&
                    x.WarehouseId == invoice.WarehouseId);

                if (inventory == null || inventory.Quantity < item.Quantity)
                    throw new InvalidOperationException("موجودی انبار برای برگشت خرید کافی نیست.");

                inventory.Quantity -= item.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    CreatedAt = DateTime.UtcNow,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    QuantityChange = -item.Quantity,
                    TransactionType = InventoryTransactionType.PurchaseRollback,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoice
                });
            }
        }

        public async Task<List<KardexDto>> GetProductKardexAsync(
    int productId,
    DateTime? fromDate,
    DateTime? toDate,
    int? warehouseId = null)
        {
            var baseQuery = _context.InventoryTransactions
                .AsNoTracking()
                .Where(x => x.ProductId == productId);

            if (warehouseId.HasValue)
            {
                baseQuery = baseQuery.Where(x =>
                    x.WarehouseId == warehouseId.Value);
            }

            decimal openingBalance = 0;

            if (fromDate.HasValue)
            {
                openingBalance = await baseQuery
                    .Where(x => x.CreatedAt < fromDate.Value)
                    .SumAsync(x => (decimal?)x.QuantityChange) ?? 0;
            }

            var query = baseQuery;

            if (fromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value
                    .Date
                    .AddDays(1)
                    .AddTicks(-1);

                query = query.Where(x =>
                    x.CreatedAt <= endDate);
            }

            /*
             * به‌جای item.Warehouse.Title،
             * مستقیماً جدول Warehouse را Join می‌کنیم.
             */
            var transactions = await
                (
                    from item in query
                    join warehouse in _context.Set<Warehouse>()
                        on item.WarehouseId equals warehouse.Id
                        into warehouseGroup
                    from warehouse in warehouseGroup.DefaultIfEmpty()
                    orderby item.CreatedAt, item.Id
                    select new
                    {
                        Item = item,

                        // اگر انبار حذف شده یا رکورد آن پیدا نشود:
                        WarehouseTitle = warehouse != null
                            ? warehouse.Title
                            : "نامشخص"
                    }
                )
                .ToListAsync();

            decimal runningBalance = openingBalance;

            var result = new List<KardexDto>();

            foreach (var transaction in transactions)
            {
                var item = transaction.Item;

                var inputQty = item.QuantityChange > 0
                    ? item.QuantityChange
                    : 0;

                var outputQty = item.QuantityChange < 0
                    ? Math.Abs(item.QuantityChange)
                    : 0;

                runningBalance += item.QuantityChange;

                result.Add(new KardexDto
                {
                    TransactionDate = item.CreatedAt,

                    TransactionTypeTitle =
                        item.TransactionType.ToPersianName(),

                    ReferenceTypeTitle =
                        item.ReferenceType.ToPersianName(),

                    WarehouseTitle =
                        transaction.WarehouseTitle,

                    ReferenceId = item.ReferenceId,

                    QuantityChange = item.QuantityChange,

                    InputQuantity = inputQty,

                    OutputQuantity = outputQty,

                    RunningBalance = runningBalance,

                    WarehouseId = item.WarehouseId,

                    Description = GetKardexDescription(
                        item.ReferenceType,
                        item.ReferenceId)
                });
            }

            return result;
        }


        private static string GetKardexDescription(InventoryReferenceType referenceType, long referenceId)
        {
            return $"ثبت شده بر اساس {referenceType.ToPersianName()} به شماره: {referenceId}";
        }

        public async Task ApplyAdjustmentAsync(AdjustmentDto dto, CancellationToken ct = default)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync(ct);

            try
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(x => x.ProductId == dto.ProductId && x.WarehouseId == dto.WarehouseId, ct);

                if (inventory == null)
                {
                    throw new Exception("رکورد موجودی برای این کالا و انبار یافت نشد.");
                }

                inventory.Quantity += dto.QuantityChange;

                var transactionLog = new InventoryTransaction
                {
                    ProductId = dto.ProductId,
                    WarehouseId = dto.WarehouseId,
                    TenantId = (int)_currentUser.TenantId,
                    FiscalYearId = (int)_currentFiscalYear.FiscalYearId,
                    QuantityChange = dto.QuantityChange,
                    TransactionType = InventoryTransactionType.Adjustment,
                    Description = dto.Description,
                    ReferenceId = dto.ReferenceId,
                    ReferenceType = InventoryReferenceType.SalesInvoice,
                    CreatedAt = DateTime.Now,
                };

                await _context.InventoryTransactions.AddAsync(transactionLog, ct);
                await _unitOfWork.SaveAsync();
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}