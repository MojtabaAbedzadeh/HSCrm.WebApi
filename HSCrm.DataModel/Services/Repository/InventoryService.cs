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

        public InventoryService(
            ApplicationDbContext context,
            IUnitOfWork unitOfWork,
            ICurrentFiscalYearService currentFiscalYear,
            ICurrentUserService currentUser) : base(context)
        {
            _context = context;
            _unitOfWork = unitOfWork;
            _currentFiscalYear = currentFiscalYear;
            _currentUser = currentUser;
        }

        /// <summary>
        /// خروج کالا از انبار بابت فاکتور فروش + محاسبه بهای تمام‌شده کالای فروش‌رفته (COGS)
        /// به روش میانگین موزون متحرک (WeightedAverage) یا اولین صادره از اولین وارده (FIFO)
        /// </summary>
        public void ApplyInventoryForSalesInvoice(
            SalesInvoice invoice,
            InventoryCostingMethod costingMethod = InventoryCostingMethod.WeightedAverage)
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
                        Quantity = 0,
                        AverageUnitCost = 0
                    };
                    _context.Inventories.Add(inventory);
                }

                if (inventory.Quantity < item.Quantity)
                    throw new InvalidOperationException("موجودی انبار برای ثبت فروش کافی نیست.");

                decimal cogsUnitCost = 0m;
                decimal cogsTotalCost = 0m;

                if (costingMethod == InventoryCostingMethod.FIFO)
                {
                    // الگوریتم FIFO: مصرف از قدیمی‌ترین پارت‌های خرید که RemainingFifoQuantity > 0 دارند
                    var openLayers = _context.InventoryTransactions
                        .Where(x => x.TenantId == invoice.TenantId
                                 && x.WarehouseId == invoice.WarehouseId
                                 && x.ProductId == item.ProductId
                                 && x.QuantityChange > 0
                                 && x.RemainingFifoQuantity > 0
                                 && !x.IsDeleted)
                        .OrderBy(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .ToList();

                    decimal remainingToConsume = item.Quantity;
                    foreach (var layer in openLayers)
                    {
                        if (remainingToConsume <= 0) break;

                        decimal consumeFromLayer = Math.Min(remainingToConsume, layer.RemainingFifoQuantity);
                        cogsTotalCost += consumeFromLayer * layer.UnitCost;
                        layer.RemainingFifoQuantity -= consumeFromLayer;
                        remainingToConsume -= consumeFromLayer;
                    }

                    // اگر بخشی از موجودی از قبل بدون لایه FIFO بوده باشد، با نرخ میانگین موزون قیمت‌گذاری می‌شود
                    if (remainingToConsume > 0)
                    {
                        cogsTotalCost += remainingToConsume * inventory.AverageUnitCost;
                    }

                    cogsTotalCost = Math.Round(cogsTotalCost, 2);
                    cogsUnitCost = item.Quantity > 0 ? Math.Round(cogsTotalCost / item.Quantity, 2) : inventory.AverageUnitCost;
                }
                else
                {
                    // الگوریتم میانگین موزون متحرک (Moving Weighted Average)
                    cogsUnitCost = inventory.AverageUnitCost;
                    cogsTotalCost = Math.Round(item.Quantity * cogsUnitCost, 2);

                    // همگام‌سازی لایه‌های FIFO برای حفظ سازگاری گزارش مقایسه‌ای
                    var openLayers = _context.InventoryTransactions
                        .Where(x => x.TenantId == invoice.TenantId
                                 && x.WarehouseId == invoice.WarehouseId
                                 && x.ProductId == item.ProductId
                                 && x.QuantityChange > 0
                                 && x.RemainingFifoQuantity > 0
                                 && !x.IsDeleted)
                        .OrderBy(x => x.CreatedAt)
                        .ThenBy(x => x.Id)
                        .ToList();

                    decimal rem = item.Quantity;
                    foreach (var layer in openLayers)
                    {
                        if (rem <= 0) break;
                        decimal take = Math.Min(rem, layer.RemainingFifoQuantity);
                        layer.RemainingFifoQuantity -= take;
                        rem -= take;
                    }
                }

                // ذخیره بهای تمام‌شده روی ردیف فاکتور فروش جهت استفاده در سند حسابداری COGS
                item.UnitCost = cogsUnitCost;
                item.TotalCost = cogsTotalCost;

                inventory.Quantity -= item.Quantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    QuantityChange = -item.Quantity,
                    UnitCost = cogsUnitCost,
                    TotalCost = cogsTotalCost,
                    RemainingFifoQuantity = 0,
                    CreatedAt = DateTime.UtcNow,
                    Description = $"خروج انبار بابت فاکتور فروش شماره {invoice.Number}",
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

                decimal returnUnitCost = item.UnitCost > 0 ? item.UnitCost : inventory.AverageUnitCost;
                decimal returnTotalCost = item.TotalCost > 0 ? item.TotalCost : Math.Round(item.Quantity * returnUnitCost, 2);

                // بازنگری میانگین موزون هنگام برگشت کالا به انبار
                decimal currentVal = Math.Max(0m, inventory.Quantity) * inventory.AverageUnitCost;
                decimal newQty = inventory.Quantity + item.Quantity;
                if (newQty > 0)
                {
                    inventory.AverageUnitCost = Math.Round((currentVal + returnTotalCost) / newQty, 2);
                }
                inventory.Quantity = newQty;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = item.Quantity,
                    UnitCost = returnUnitCost,
                    TotalCost = returnTotalCost,
                    RemainingFifoQuantity = item.Quantity,
                    Description = $"برگشت به انبار بابت ابطال/ویرایش فاکتور فروش شماره {invoice.Number}",
                    TransactionType = InventoryTransactionType.SaleRollback,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.SalesInvoice
                });
            }
        }

        /// <summary>
        /// ورود کالا به انبار بابت فاکتور خرید + بروزرسانی خودکار نرخ میانگین موزون متحرک و ثبت لایه FIFO
        /// </summary>
        public Task ApplyPurchaseInvoice(PurchaseInvoice invoice)
            => ApplyInventoryForPurchaseInvoice(invoice);

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
                        Quantity = 0,
                        AverageUnitCost = 0
                    };

                    await _context.Inventories.AddAsync(inventory);
                }

                // محاسبه بهای تمام‌شده خالص خرید (پس از کسر تخفیف ردیف، بدون مالیات ارزش افزوده قابل استرداد)
                decimal grossCost = item.Quantity * item.UnitPricePurchase;
                decimal netLineCost = Math.Max(0m, grossCost - (item.Discount ?? 0m));
                decimal purchaseUnitCost = item.Quantity > 0
                    ? Math.Round(netLineCost / item.Quantity, 2)
                    : item.UnitPricePurchase;

                // فرمول میانگین موزون متحرک:
                // NewAvgCost = ((OldQty * OldAvgCost) + (PurchaseQty * PurchaseUnitCost)) / (OldQty + PurchaseQty)
                decimal existingValue = Math.Max(0m, inventory.Quantity) * inventory.AverageUnitCost;
                decimal newTotalQty = inventory.Quantity + item.Quantity;

                if (newTotalQty > 0)
                {
                    inventory.AverageUnitCost = Math.Round((existingValue + netLineCost) / newTotalQty, 2);
                }
                else
                {
                    inventory.AverageUnitCost = purchaseUnitCost;
                }

                inventory.Quantity = newTotalQty;

                await _context.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    CreatedAt = DateTime.UtcNow,
                    ProductId = item.ProductId,
                    QuantityChange = item.Quantity,
                    UnitCost = purchaseUnitCost,
                    TotalCost = netLineCost,
                    RemainingFifoQuantity = item.Quantity, // آماده مصرف در روش FIFO
                    Description = $"ورود به انبار بابت فاکتور خرید شماره {invoice.Number}",
                    TransactionType = InventoryTransactionType.Purchase,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoice
                });
            }
        }

        /// <summary>
        /// ورود مجدد کالا به انبار بابت فاکتور برگشت از فروش + بازنگری میانگین موزون و لایه FIFO + ثبت بهای تمام‌شده برگشتی
        /// </summary>
        public async Task ApplyInventoryForSalesReturnAsync(SalesReturnInvoice returnInvoice, CancellationToken ct = default)
        {
            if (returnInvoice == null || returnInvoice.Items == null || !returnInvoice.Items.Any())
                return;

            if (returnInvoice.Status != InvoiceStatus.Final)
                return;

            List<SalesInvoiceItem> originalSaleItems = new();
            if (returnInvoice.SalesInvoiceId.HasValue && returnInvoice.SalesInvoiceId.Value > 0)
            {
                originalSaleItems = await _context.SalesInvoiceItems
                    .AsNoTracking()
                    .Where(x => x.SalesInvoiceId == returnInvoice.SalesInvoiceId.Value && !x.IsDeleted)
                    .ToListAsync(ct);
            }

            foreach (var item in returnInvoice.Items)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == returnInvoice.TenantId &&
                    x.WarehouseId == returnInvoice.WarehouseId, ct);

                if (inventory == null)
                {
                    inventory = new Inventory
                    {
                        ProductId = item.ProductId,
                        TenantId = returnInvoice.TenantId,
                        WarehouseId = returnInvoice.WarehouseId,
                        CreatedAt = DateTime.UtcNow,
                        Quantity = 0,
                        AverageUnitCost = 0
                    };
                    await _context.Inventories.AddAsync(inventory, ct);
                }

                // تعیین نرخ بهای تمام‌شده کالای برگشتی از فروش:
                // ۱. اگر فاکتور فروش مرجع انتخاب شده و بهای تمام‌شده (UnitCost) دارد، دقیقاً با همان نرخ به انبار برمی‌گردد
                // ۲. در غیر این صورت از نرخ میانگین موزون فعلی انبار (یا نرخ ارسالی) استفاده می‌شود
                var matchedOriginal = originalSaleItems.FirstOrDefault(x => x.ProductId == item.ProductId && x.UnitCost > 0);
                decimal returnUnitCost = matchedOriginal != null
                    ? matchedOriginal.UnitCost
                    : (item.UnitCost > 0 ? item.UnitCost : (inventory.AverageUnitCost > 0 ? inventory.AverageUnitCost : item.UnitPriceReturn));

                decimal returnTotalCost = Math.Round(item.Quantity * returnUnitCost, 2);

                item.UnitCost = returnUnitCost;
                item.TotalCost = returnTotalCost;

                decimal existingVal = Math.Max(0m, inventory.Quantity) * inventory.AverageUnitCost;
                decimal newQty = inventory.Quantity + item.Quantity;

                if (newQty > 0)
                {
                    inventory.AverageUnitCost = Math.Round((existingVal + returnTotalCost) / newQty, 2);
                }
                else
                {
                    inventory.AverageUnitCost = returnUnitCost;
                }

                inventory.Quantity = newQty;

                await _context.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    FiscalYearId = returnInvoice.FiscalYearId,
                    WarehouseId = returnInvoice.WarehouseId,
                    TenantId = returnInvoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = item.Quantity,
                    UnitCost = returnUnitCost,
                    TotalCost = returnTotalCost,
                    RemainingFifoQuantity = item.Quantity,
                    Description = $"ورود مجدد به انبار بابت فاکتور برگشت از فروش شماره {returnInvoice.Number}",
                    TransactionType = InventoryTransactionType.ReturnSale,
                    ReferenceId = returnInvoice.Id,
                    ReferenceType = InventoryReferenceType.SalesReturnInvoice
                }, ct);
            }
        }

        /// <summary>
        /// ابطال فاکتور برگشت از فروش (کسر مجدد کالا از انبار)
        /// </summary>
        public void RollbackInventoryForSalesReturn(SalesReturnInvoice returnInvoice)
        {
            if (returnInvoice == null || returnInvoice.Items == null || !returnInvoice.Items.Any())
                return;

            if (returnInvoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in returnInvoice.Items)
            {
                var inventory = _context.Inventories.FirstOrDefault(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == returnInvoice.TenantId &&
                    x.WarehouseId == returnInvoice.WarehouseId);

                if (inventory == null || inventory.Quantity < item.Quantity)
                    throw new InvalidOperationException("موجودی انبار برای ابطال فاکتور برگشت از فروش کافی نیست.");

                decimal unitCost = item.UnitCost > 0 ? item.UnitCost : inventory.AverageUnitCost;
                decimal totalCost = item.TotalCost > 0 ? item.TotalCost : Math.Round(item.Quantity * unitCost, 2);

                decimal existingVal = inventory.Quantity * inventory.AverageUnitCost;
                decimal newQty = inventory.Quantity - item.Quantity;
                if (newQty > 0)
                {
                    decimal remVal = Math.Max(0m, existingVal - totalCost);
                    inventory.AverageUnitCost = Math.Round(remVal / newQty, 2);
                }

                inventory.Quantity = newQty;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = returnInvoice.FiscalYearId,
                    WarehouseId = returnInvoice.WarehouseId,
                    TenantId = returnInvoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = -item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    RemainingFifoQuantity = 0,
                    Description = $"خروج از انبار بابت ابطال فاکتور برگشت از فروش شماره {returnInvoice.Number}",
                    TransactionType = InventoryTransactionType.Sale,
                    ReferenceId = returnInvoice.Id,
                    ReferenceType = InventoryReferenceType.SalesReturnInvoice
                });
            }
        }

        /// <summary>
        /// خروج کالا از انبار بابت فاکتور برگشت از خرید (عودت کالا به تأمین‌کننده) + اصلاح میانگین موزون و لایه‌های FIFO
        /// </summary>
        public async Task ApplyInventoryForPurchaseReturnAsync(PurchaseReturnInvoice returnInvoice, CancellationToken ct = default)
        {
            if (returnInvoice == null || returnInvoice.Items == null || !returnInvoice.Items.Any())
                return;

            if (returnInvoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in returnInvoice.Items)
            {
                var inventory = await _context.Inventories.FirstOrDefaultAsync(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == returnInvoice.TenantId &&
                    x.WarehouseId == returnInvoice.WarehouseId, ct);

                if (inventory == null || inventory.Quantity < item.Quantity)
                    throw new InvalidOperationException("موجودی انبار برای ثبت فاکتور برگشت از خرید کافی نیست.");

                decimal grossReturn = item.Quantity * item.UnitPriceReturn;
                decimal netLineReturn = Math.Max(0m, grossReturn - (item.Discount ?? 0m));
                decimal returnUnitCost = item.Quantity > 0
                    ? Math.Round(netLineReturn / item.Quantity, 2)
                    : item.UnitPriceReturn;

                decimal existingVal = inventory.Quantity * inventory.AverageUnitCost;
                decimal newQty = inventory.Quantity - item.Quantity;

                if (newQty > 0)
                {
                    decimal remainingVal = Math.Max(0m, existingVal - netLineReturn);
                    inventory.AverageUnitCost = Math.Round(remainingVal / newQty, 2);
                }

                inventory.Quantity = newQty;

                // کسر از لایه‌های باز FIFO (ابتدا لایه فاکتور خرید مرجع در صورت وجود، سپس قدیمی‌ترین لایه‌ها)
                var openLayers = await _context.InventoryTransactions
                    .Where(x => x.TenantId == returnInvoice.TenantId
                             && x.WarehouseId == returnInvoice.WarehouseId
                             && x.ProductId == item.ProductId
                             && x.QuantityChange > 0
                             && x.RemainingFifoQuantity > 0
                             && !x.IsDeleted)
                    .OrderByDescending(x => returnInvoice.PurchaseInvoiceId.HasValue
                                         && x.ReferenceType == InventoryReferenceType.PurchaseInvoice
                                         && x.ReferenceId == returnInvoice.PurchaseInvoiceId.Value)
                    .ThenBy(x => x.CreatedAt)
                    .ThenBy(x => x.Id)
                    .ToListAsync(ct);

                decimal remToReduce = item.Quantity;
                foreach (var layer in openLayers)
                {
                    if (remToReduce <= 0) break;
                    decimal take = Math.Min(remToReduce, layer.RemainingFifoQuantity);
                    layer.RemainingFifoQuantity -= take;
                    remToReduce -= take;
                }

                await _context.InventoryTransactions.AddAsync(new InventoryTransaction
                {
                    FiscalYearId = returnInvoice.FiscalYearId,
                    WarehouseId = returnInvoice.WarehouseId,
                    TenantId = returnInvoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = -item.Quantity,
                    UnitCost = returnUnitCost,
                    TotalCost = netLineReturn,
                    RemainingFifoQuantity = 0,
                    Description = $"خروج از انبار بابت فاکتور برگشت از خرید شماره {returnInvoice.Number}",
                    TransactionType = InventoryTransactionType.ReturnPurchase,
                    ReferenceId = returnInvoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseReturnInvoice
                }, ct);
            }
        }

        /// <summary>
        /// ابطال فاکتور برگشت از خرید (ورود مجدد کالا به انبار)
        /// </summary>
        public void RollbackInventoryForPurchaseReturn(PurchaseReturnInvoice returnInvoice)
        {
            if (returnInvoice == null || returnInvoice.Items == null || !returnInvoice.Items.Any())
                return;

            if (returnInvoice.Status != InvoiceStatus.Final)
                return;

            foreach (var item in returnInvoice.Items)
            {
                var inventory = _context.Inventories.FirstOrDefault(x =>
                    x.ProductId == item.ProductId &&
                    x.TenantId == returnInvoice.TenantId &&
                    x.WarehouseId == returnInvoice.WarehouseId);

                if (inventory == null)
                    throw new InvalidOperationException("رکورد موجودی انبار برای ابطال برگشت از خرید یافت نشد.");

                decimal grossReturn = item.Quantity * item.UnitPriceReturn;
                decimal netLineReturn = Math.Max(0m, grossReturn - (item.Discount ?? 0m));
                decimal returnUnitCost = item.Quantity > 0
                    ? Math.Round(netLineReturn / item.Quantity, 2)
                    : item.UnitPriceReturn;

                decimal existingVal = Math.Max(0m, inventory.Quantity) * inventory.AverageUnitCost;
                decimal newQty = inventory.Quantity + item.Quantity;

                if (newQty > 0)
                {
                    inventory.AverageUnitCost = Math.Round((existingVal + netLineReturn) / newQty, 2);
                }

                inventory.Quantity = newQty;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = returnInvoice.FiscalYearId,
                    WarehouseId = returnInvoice.WarehouseId,
                    TenantId = returnInvoice.TenantId,
                    ProductId = item.ProductId,
                    CreatedAt = DateTime.UtcNow,
                    QuantityChange = item.Quantity,
                    UnitCost = returnUnitCost,
                    TotalCost = netLineReturn,
                    RemainingFifoQuantity = item.Quantity,
                    Description = $"ورود مجدد به انبار بابت ابطال فاکتور برگشت از خرید شماره {returnInvoice.Number}",
                    TransactionType = InventoryTransactionType.Purchase,
                    ReferenceId = returnInvoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseReturnInvoice
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

                decimal grossCost = item.Quantity * item.UnitPricePurchase;
                decimal netLineCost = Math.Max(0m, grossCost - (item.Discount ?? 0m));
                decimal purchaseUnitCost = item.Quantity > 0
                    ? Math.Round(netLineCost / item.Quantity, 2)
                    : item.UnitPricePurchase;

                decimal existingValue = inventory.Quantity * inventory.AverageUnitCost;
                decimal newTotalQty = inventory.Quantity - item.Quantity;

                if (newTotalQty > 0)
                {
                    decimal remainingValue = Math.Max(0m, existingValue - netLineCost);
                    inventory.AverageUnitCost = Math.Round(remainingValue / newTotalQty, 2);
                }

                inventory.Quantity = newTotalQty;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    FiscalYearId = invoice.FiscalYearId,
                    CreatedAt = DateTime.UtcNow,
                    WarehouseId = invoice.WarehouseId,
                    TenantId = invoice.TenantId,
                    ProductId = item.ProductId,
                    QuantityChange = -item.Quantity,
                    UnitCost = purchaseUnitCost,
                    TotalCost = netLineCost,
                    RemainingFifoQuantity = 0,
                    Description = $"برگشت از انبار بابت ابطال فاکتور خرید شماره {invoice.Number}",
                    TransactionType = InventoryTransactionType.PurchaseRollback,
                    ReferenceId = invoice.Id,
                    ReferenceType = InventoryReferenceType.PurchaseInvoice
                });
            }
        }

        /// <summary>
        /// گزارش کاردکس تعدادی و ریالی کالا (شامل فی، مبلغ کل، نرخ میانگین در گردش و مانده ریالی)
        /// </summary>
        public async Task<List<KardexDto>> GetProductKardexAsync(int productId, DateTime? fromDate, DateTime? toDate, int? warehouseId = null)
        {
            var baseQuery = _context.InventoryTransactions
                .AsNoTracking()
                .Where(x => x.ProductId == productId && !x.IsDeleted);

            if (warehouseId.HasValue)
            {
                baseQuery = baseQuery.Where(x => x.WarehouseId == warehouseId.Value);
            }

            decimal openingBalance = 0;
            decimal openingValue = 0;

            if (fromDate.HasValue)
            {
                var priorItems = await baseQuery
                    .Where(x => x.CreatedAt < fromDate.Value)
                    .ToListAsync();

                openingBalance = priorItems.Sum(x => x.QuantityChange);
                openingValue = priorItems.Sum(x => x.QuantityChange >= 0 ? x.TotalCost : -x.TotalCost);
            }

            var query = baseQuery;

            if (fromDate.HasValue)
            {
                query = query.Where(x => x.CreatedAt >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(x => x.CreatedAt <= endDate);
            }

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
                        WarehouseTitle = warehouse != null ? warehouse.Title : "نامشخص"
                    }
                )
                .ToListAsync();

            decimal runningBalance = openingBalance;
            decimal runningValue = Math.Max(0m, openingValue);
            var result = new List<KardexDto>();

            foreach (var transaction in transactions)
            {
                var item = transaction.Item;

                var inputQty = item.QuantityChange > 0 ? item.QuantityChange : 0;
                var outputQty = item.QuantityChange < 0 ? Math.Abs(item.QuantityChange) : 0;

                runningBalance += item.QuantityChange;
                runningValue += item.QuantityChange >= 0 ? item.TotalCost : -item.TotalCost;
                if (runningBalance <= 0) runningValue = 0m;

                decimal runningUnitCost = runningBalance > 0
                    ? Math.Round(runningValue / runningBalance, 2)
                    : item.UnitCost;

                result.Add(new KardexDto
                {
                    TransactionDate = item.CreatedAt,
                    TransactionTypeTitle = item.TransactionType.ToPersianName(),
                    ReferenceTypeTitle = item.ReferenceType.ToPersianName(),
                    WarehouseTitle = transaction.WarehouseTitle,
                    ReferenceId = item.ReferenceId,
                    QuantityChange = item.QuantityChange,
                    InputQuantity = inputQty,
                    OutputQuantity = outputQty,
                    RunningBalance = runningBalance,
                    UnitCost = item.UnitCost,
                    TotalCost = item.TotalCost,
                    RunningUnitCost = runningUnitCost,
                    RunningValueBalance = Math.Round(runningValue, 2),
                    WarehouseId = item.WarehouseId,
                    Description = !string.IsNullOrWhiteSpace(item.Description)
                        ? item.Description
                        : GetKardexDescription(item.ReferenceType, item.ReferenceId)
                });
            }

            return result;
        }

        /// <summary>
        /// گزارش ارزش‌گذاری موجودی انبار به تفکیک هر دو روش میانگین موزون متحرک و FIFO
        /// </summary>
        public async Task<List<InventoryValuationDto>> GetInventoryValuationAsync(int? warehouseId = null, CancellationToken ct = default)
        {
            var invQuery = _context.Inventories
                .AsNoTracking()
                .Include(x => x.Product)
                .Include(x => x.Warehouse)
                .Where(x => !x.IsDeleted && x.Quantity > 0);

            if (warehouseId.HasValue)
                invQuery = invQuery.Where(x => x.WarehouseId == warehouseId.Value);

            var inventories = await invQuery.OrderBy(x => x.ProductId).ToListAsync(ct);

            var openFifoLayers = await _context.InventoryTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.QuantityChange > 0 && x.RemainingFifoQuantity > 0)
                .ToListAsync(ct);

            var result = new List<InventoryValuationDto>();
            foreach (var inv in inventories)
            {
                var productLayers = openFifoLayers
                    .Where(l => l.ProductId == inv.ProductId && l.WarehouseId == inv.WarehouseId)
                    .OrderBy(l => l.CreatedAt)
                    .ThenBy(l => l.Id)
                    .ToList();

                decimal fifoVal = productLayers.Sum(l => l.RemainingFifoQuantity * l.UnitCost);
                decimal coveredQty = productLayers.Sum(l => l.RemainingFifoQuantity);
                if (coveredQty < inv.Quantity)
                {
                    fifoVal += (inv.Quantity - coveredQty) * inv.AverageUnitCost;
                }

                result.Add(new InventoryValuationDto
                {
                    ProductId = inv.ProductId,
                    ProductTitle = inv.Product?.ProductTitle ?? $"کالای #{inv.ProductId}",
                    WarehouseId = inv.WarehouseId,
                    WarehouseTitle = inv.Warehouse?.Title ?? $"انبار #{inv.WarehouseId}",
                    CurrentQuantity = inv.Quantity,
                    WeightedAverageUnitCost = inv.AverageUnitCost,
                    FifoTotalValue = Math.Round(fifoVal, 2)
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
                var inventory = await _context.Inventories.FirstOrDefaultAsync(
                    x => x.ProductId == dto.ProductId && x.WarehouseId == dto.WarehouseId, ct);

                if (inventory == null)
                {
                    throw new Exception("رکورد موجودی برای این کالا و انبار یافت نشد.");
                }

                decimal unitCost = inventory.AverageUnitCost;
                decimal totalCost = Math.Round(Math.Abs(dto.QuantityChange) * unitCost, 2);

                inventory.Quantity += dto.QuantityChange;

                var transactionLog = new InventoryTransaction
                {
                    ProductId = dto.ProductId,
                    WarehouseId = dto.WarehouseId,
                    TenantId = (int)_currentUser.TenantId,
                    FiscalYearId = (int)_currentFiscalYear.FiscalYearId,
                    QuantityChange = dto.QuantityChange,
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    RemainingFifoQuantity = dto.QuantityChange > 0 ? dto.QuantityChange : 0,
                    TransactionType = InventoryTransactionType.Adjustment,
                    Description = dto.Description,
                    ReferenceId = dto.ReferenceId,
                    ReferenceType = InventoryReferenceType.Adjustment,
                    CreatedAt = DateTime.Now,
                };

                await _context.InventoryTransactions.AddAsync(transactionLog, ct);
                await _unitOfWork.SaveAsync();
                await transaction.CommitAsync(ct);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
