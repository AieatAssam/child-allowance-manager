using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository.Paging;

namespace ChildAllowanceManager.Common.Interfaces;

public interface ITransactionService
{
    ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default);
    
    ValueTask<IPageQueryResult<AllowanceTransaction>> GetPagedTransactionsForChild(string childId, string tenantId,
        int page,
        int pageSize,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default);


    ValueTask<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default);
    ValueTask<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default);
    ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default);
}