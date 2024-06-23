using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface ITransactionService
{
    Task<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default);

    Task<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default);
    Task<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default);
}