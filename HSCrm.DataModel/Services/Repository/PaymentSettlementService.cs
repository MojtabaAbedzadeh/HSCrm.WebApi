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
            var receipts = _db.Receipts
                .AsNoTracking()
                .Where(x => x.SalesInvoiceId == salesInvoiceId && !x.IsDeleted)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.ReceiptDate
                })
                .ToList();

            var allCheques = _db.Cheques
                .AsNoTracking()
                .Where(x => x.SalesInvoiceId == salesInvoiceId
                            && x.Type == ChequeType.Receivable
                            && !x.IsDeleted)
                .ToList();

            var activeCheques = allCheques
                .Where(x => x.Status != ChequeStatus.Bounced && x.Status != ChequeStatus.Returned)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Status == ChequeStatus.Cleared || x.Status == ChequeStatus.EndorsedToSupplier
                        ? "چک (وصول/خرج شده)"
                        : "چک دریافتنی (در جریان)",
                    ReferenceNo = x.ChequeNumber,
                    PaymentDate = x.IssueDate
                })
                .ToList();

            var cashPaid = receipts.Sum(x => x.Amount);
            var pendingCheques = allCheques
                .Where(x => x.Status == ChequeStatus.InSafe || x.Status == ChequeStatus.InBankForCollection)
                .Sum(x => x.Amount);
            var clearedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Cleared || x.Status == ChequeStatus.EndorsedToSupplier)
                .Sum(x => x.Amount);
            var bouncedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Bounced || x.Status == ChequeStatus.Returned)
                .Sum(x => x.Amount);

            var allItems = receipts
                .Concat(activeCheques)
                .OrderByDescending(x => x.PaymentDate)
                .ToList();

            return BuildSummary(totalAmount, allItems, cashPaid, pendingCheques, clearedCheques, bouncedCheques, salesInvoiceId: salesInvoiceId);
        }

        public SettlementSummaryDto BuildPurchaseInvoiceSummary(int purchaseInvoiceId, decimal totalAmount)
        {
            var payments = _db.Payments
                .AsNoTracking()
                .Where(x => x.PurchaseInvoiceId == purchaseInvoiceId && !x.IsDeleted)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.PaymentDate
                })
                .ToList();

            var allCheques = _db.Cheques
                .AsNoTracking()
                .Where(x => x.PurchaseInvoiceId == purchaseInvoiceId
                            && x.Type == ChequeType.Payable
                            && !x.IsDeleted)
                .ToList();

            var activeCheques = allCheques
                .Where(x => x.Status != ChequeStatus.Bounced && x.Status != ChequeStatus.Returned)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Status == ChequeStatus.Cleared ? "چک (پاس شده)" : "چک پرداختنی (در جریان)",
                    ReferenceNo = x.ChequeNumber,
                    PaymentDate = x.IssueDate
                })
                .ToList();

            var cashPaid = payments.Sum(x => x.Amount);
            var pendingCheques = allCheques
                .Where(x => x.Status == ChequeStatus.InSafe || x.Status == ChequeStatus.InBankForCollection)
                .Sum(x => x.Amount);
            var clearedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Cleared)
                .Sum(x => x.Amount);
            var bouncedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Bounced || x.Status == ChequeStatus.Returned)
                .Sum(x => x.Amount);

            var allItems = payments
                .Concat(activeCheques)
                .OrderByDescending(x => x.PaymentDate)
                .ToList();

            return BuildSummary(totalAmount, allItems, cashPaid, pendingCheques, clearedCheques, bouncedCheques, purchaseInvoiceId: purchaseInvoiceId);
        }

        public SettlementSummaryDto BuildExpenseSummary(int expenseId, decimal totalAmount)
        {
            var payments = _db.Payments
                .AsNoTracking()
                .Where(x => x.ExpenseId == expenseId && !x.IsDeleted)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Method,
                    ReferenceNo = x.ReferenceNo,
                    PaymentDate = x.PaymentDate
                })
                .ToList();

            var allCheques = _db.Cheques
                .AsNoTracking()
                .Where(x => x.ExpenseId == expenseId
                            && x.Type == ChequeType.Payable
                            && !x.IsDeleted)
                .ToList();

            var activeCheques = allCheques
                .Where(x => x.Status != ChequeStatus.Bounced && x.Status != ChequeStatus.Returned)
                .Select(x => new PaymentItemDto
                {
                    PaymentId = x.Id,
                    Amount = x.Amount,
                    Method = x.Status == ChequeStatus.Cleared ? "چک (پاس شده)" : "چک پرداختنی (در جریان)",
                    ReferenceNo = x.ChequeNumber,
                    PaymentDate = x.IssueDate
                })
                .ToList();

            var cashPaid = payments.Sum(x => x.Amount);
            var pendingCheques = allCheques
                .Where(x => x.Status == ChequeStatus.InSafe || x.Status == ChequeStatus.InBankForCollection)
                .Sum(x => x.Amount);
            var clearedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Cleared)
                .Sum(x => x.Amount);
            var bouncedCheques = allCheques
                .Where(x => x.Status == ChequeStatus.Bounced || x.Status == ChequeStatus.Returned)
                .Sum(x => x.Amount);

            var allItems = payments
                .Concat(activeCheques)
                .OrderByDescending(x => x.PaymentDate)
                .ToList();

            return BuildSummary(totalAmount, allItems, cashPaid, pendingCheques, clearedCheques, bouncedCheques, expenseId: expenseId);
        }

        private static SettlementSummaryDto BuildSummary(
            decimal totalAmount,
            List<PaymentItemDto> payments,
            decimal cashPaidAmount,
            decimal pendingChequesAmount,
            decimal clearedChequesAmount,
            decimal bouncedChequesAmount,
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
                CashPaidAmount = cashPaidAmount,
                PendingChequesAmount = pendingChequesAmount,
                ClearedChequesAmount = clearedChequesAmount,
                BouncedChequesAmount = bouncedChequesAmount,
                SettlementStatus = status,
                Payments = payments
            };
        }
    }
}
