using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Specification;

namespace ChildAllowanceManager.Services;

public class TransactionService(
    IRepository<AllowanceTransaction> transactionRepository,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default)
    {
        var transactionResult = await transactionRepository.QueryAsync(new ChildTransactionOrderedByDateDescending(childId, tenantId, ignoreDailyAllowance),
            cancellationToken);
        return transactionResult.Items;
    }

    public async Task<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.QueryAsync(new ChildTransactionOrderedByDateDescending(childId, tenantId, false), cancellationToken);
        if (transaction.Items.Any())
        {
            return transaction.Items.First().Balance;
        }

        return 0m;
    }
    
    public async Task<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.TransactionTimestamp = DateTimeOffset.UtcNow;

        var oldBalance = await GetBalanceForChild(transaction.ChildId, transaction.TenantId, cancellationToken);
        transaction.CreatedTimestamp = DateTimeOffset.UtcNow;
        transaction.Balance = oldBalance + transaction.TransactionAmount;

        return await transactionRepository.CreateAsync(transaction, cancellationToken);
    }

    class ChildTransactionOrderedByDateDescending : DefaultSpecification<AllowanceTransaction>
    {
        public ChildTransactionOrderedByDateDescending(string childId, string tenantId, bool ignoreDailyAllowance)
        {
            Query.Where(x => x.TenantId == tenantId && x.ChildId == childId)
                .OrderByDescending(x => x.TransactionTimestamp);
            if (ignoreDailyAllowance)
            {
                Query.Where(x => x.TransactionType != TransactionType.DailyAllowance);
            }
        }
    }
}