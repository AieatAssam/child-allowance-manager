using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Paging;
using Microsoft.Azure.CosmosRepository.Providers;
using Microsoft.Azure.CosmosRepository.Specification;

namespace ChildAllowanceManager.Services;

public class TransactionService(
    IRepository<AllowanceTransaction> transactionRepository,
    IHubContext<NotificationHub> notificationHub,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false,
        CancellationToken cancellationToken = default)
    {
        var transactionResult = await transactionRepository.QueryAsync(
            new ChildTransactionOrderedByDateDescending(childId, tenantId, ignoreDailyAllowance),
            cancellationToken);
        return transactionResult.Items;
    }

    public ValueTask<IPageQueryResult<AllowanceTransaction>> GetPagedTransactionsForChild(string childId,
        string tenantId, int page, int pageSize,
        bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default)
    {
        return transactionRepository.QueryAsync(
            new ChildTransactionOrderedByDateDescendingPaged(childId, tenantId, false, page, pageSize),
            cancellationToken);
    }

    public async ValueTask<AllowanceTransaction?> GetLatestRegularTransactionForChild(string childId, string tenantId,
        CancellationToken cancellationToken = default)
    {
        var transactionResult = await transactionRepository.QueryAsync(
            new ChildTransactionOrderedByDateDescending(childId, tenantId, false)
                .WithTransactionTypes(TransactionType.DailyAllowance, TransactionType.BirthdayAllowance)
                .WithPageSize(1),
            cancellationToken);
        return transactionResult.Items.FirstOrDefault();
    }

    public async ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(string childId, string tenantId,
        CancellationToken cancellationToken = default)
    {
        var transactionResult = await transactionRepository.QueryAsync(
            new ChildTransactionOrderedByDateDescending(childId, tenantId, false)
                .WithPageSize(1),
            cancellationToken);
        return transactionResult.Items.FirstOrDefault();
    }

    public async ValueTask<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default)
    {
        var transaction =
            await transactionRepository.QueryAsync(
                new ChildTransactionOrderedByDateDescending(childId, tenantId, false), cancellationToken);
        if (transaction.Items.Any())
        {
            return transaction.Items.First().Balance;
        }

        return 0m;
    }
    
    public async ValueTask<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        transaction.TransactionTimestamp = DateTimeOffset.UtcNow;

        var oldBalance = await GetBalanceForChild(transaction.ChildId, transaction.TenantId, cancellationToken);
        transaction.CreatedTimestamp = DateTimeOffset.UtcNow;
        transaction.Balance = oldBalance + transaction.TransactionAmount;

        var result = await transactionRepository.CreateAsync(transaction, cancellationToken);
        
        // notify connected clients to update themselves
        await notificationHub.Clients.Group(transaction.TenantId)
            .SendAsync(NotificationHub.AllowanceUpdated, cancellationToken);
        return result;
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

        public ChildTransactionOrderedByDateDescending WithTransactionTypes(params TransactionType[] types)
        {
            if (types.Any())
            {
                Query.Where(x => types.Contains(x.TransactionType));
            }

            return this;
        }
        
        public ChildTransactionOrderedByDateDescending WithPageSize(int pageSize)
        {
            Query.PageSize(pageSize);
            return this;
        }
        
        public ChildTransactionOrderedByDateDescending WithPageNumber(int pageNumber)
        {
            Query.PageNumber(pageNumber);
            return this;
        }
    }
    
    class ChildTransactionOrderedByDateDescendingPaged : OffsetByPageNumberSpecification<AllowanceTransaction>
    {
        public ChildTransactionOrderedByDateDescendingPaged(string childId, string tenantId, bool ignoreDailyAllowance, int pageNumber, int pageSize) : base(pageNumber, pageSize)
        {
            var typeName = nameof(AllowanceTransaction);
            Query.Where(x => x.TenantId == tenantId && x.ChildId == childId
                && x.Type == typeName) // workaround for https://github.com/IEvangelist/azure-cosmos-dotnet-repository/issues/408
                .OrderByDescending(x => x.TransactionTimestamp);
            if (ignoreDailyAllowance)
            {
                Query.Where(x => x.TransactionType != TransactionType.DailyAllowance);
            }
        }
    }
}