using ChildAllowanceManager.Common.Models;

namespace ChildAllowanceManager.Common.Interfaces;

public interface ITransactionService
{
    ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default);
    
    ValueTask<PagedResult<AllowanceTransaction>> GetPagedTransactionsForChild(string childId, string tenantId,
        int page,
        int pageSize,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default);


    ValueTask<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default);
    ValueTask<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default);
    ValueTask<AllowanceTransaction?> GetLatestRegularTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default);
    ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default);

    ValueTask<IEnumerable<BalanceHistoryEntry>> GetBalanceHistoryForChild(string childId, string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken);

    /// Corrects a past transaction by writing a new reversing transaction.
    ValueTask<AllowanceTransaction> ReverseTransactionAsync(string transactionId, string tenantId, string reason,
        string? requestId, CancellationToken cancellationToken = default);

    /// Full transaction history for one child as RFC 4180 CSV, newest last.
    ValueTask<string> ExportTransactionsCsvAsync(string childId, string tenantId,
        CancellationToken cancellationToken = default);
}
