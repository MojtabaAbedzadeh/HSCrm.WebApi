using HSCrm.DataModel.Context;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities.Models.Dto;
using Microsoft.EntityFrameworkCore;
using static HSCrm.Common.PublicTools.Enums;

namespace HSCrm.DataModel.Services.Repository
{
    public class PaymentSettlementService : IPaymentSettlementService
    {
        private readonly ApplicationDbContext _db;

        public PaymentSettlementService(ApplicationDbContext db)
        {
            _db = db;
        }

        public SettlementSummaryDto BuildSalesInvoiceSummary(int salesInvoiceId, decimal totalAmount)
        {
            // نکته: در معماری جدید، دریافتی‌های فاکتور فروش در موجودیت Receipt ذخیره می‌شوند
            var receipts = _db.Receipts
                .AsNoTracking()
                .Where(x => x.SalesInvoiceId == salesInvoiceId)
                .OrderByDescending(x => x.ReceiptDate) // فرض بر این است که خاصیت ReceiptDate داری
                .Select(x => new PaymentItemDto // همچنان از همان DTO استفاده می‌کنی یا اگر خواستی PaymentItemDto را به ReceiptItemDto تغییر نام بده
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.ReceiptDate
                })
                .ToList();

            // متد BuildSummary باید پارامتر receipts را بگیرد
            return BuildSummary(totalAmount, receipts, salesInvoiceId: salesInvoiceId);
        }


        public SettlementSummaryDto BuildPurchaseInvoiceSummary(int purchaseInvoiceId, decimal totalAmount)
        {
            var payments = _db.Payments
                .AsNoTracking()
                .Where(x => x.PurchaseInvoiceId == purchaseInvoiceId)
                .OrderByDescending(x => x.PaymentDate)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.PaymentDate
                })
                .ToList();

            return BuildSummary(totalAmount, payments, purchaseInvoiceId: purchaseInvoiceId);
        }

        public SettlementSummaryDto BuildExpenseSummary(int expenseId, decimal totalAmount)
        {
            var payments = _db.Payments
                .AsNoTracking()
                .Where(x => x.ExpenseId == expenseId)
                .OrderByDescending(x => x.PaymentDate)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.PaymentDate
                })
                .ToList();

            return BuildSummary(totalAmount, payments, expenseId: expenseId);
        }

        private static SettlementSummaryDto BuildSummary(
            decimal totalAmount,
            List<PaymentItemDto> payments,
            int? salesInvoiceId = null,
            int? purchaseInvoiceId = null,
            int? expenseId = null)
        {
            var paidAmount = payments.Sum(x => x.Amount);
            var remainingAmount = totalAmount - paidAmount;

            if (remainingAmount < 0)
                remainingAmount = 0;

            var status = remainingAmount <= 0
                ? SettlementStatus.Paid
                : paidAmount > 0
                    ? SettlementStatus.Partial
                    : SettlementStatus.Unpaid;

            return new SettlementSummaryDto
            {
                SalesInvoiceId = salesInvoiceId,
                PurchaseInvoiceId = purchaseInvoiceId,
                ExpenseId = expenseId,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount,
                SettlementStatus = status,
                Payments = payments
            };
        }
    }
}