using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ChildAllowanceManager.Tests;

internal sealed class RecordingTenantService : ITenantService
{
    public List<TenantConfiguration> Tenants { get; } = [];
    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public ValueTask<IEnumerable<TenantConfiguration>> GetTenants(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IEnumerable<TenantConfiguration>>(Tenants.ToArray());

    public ValueTask<TenantConfiguration?> GetTenant(string id, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Tenants.SingleOrDefault(x => x.Id == id));

    public ValueTask<TenantConfiguration?> GetTenantBySuffix(string urlSuffix, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Tenants.SingleOrDefault(x => x.UrlSuffix == urlSuffix));

    public ValueTask<TenantConfiguration> AddTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        AddCalls++;
        Tenants.Add(tenant);
        return ValueTask.FromResult(tenant);
    }

    public ValueTask<TenantConfiguration> UpdateTenant(TenantConfiguration tenant, CancellationToken cancellationToken = default)
    {
        UpdateCalls++;
        Replace(Tenants, tenant);
        return ValueTask.FromResult(tenant);
    }

    public ValueTask<bool> DeleteTenant(string id, CancellationToken cancellationToken = default)
    {
        DeleteCalls++;
        Tenants.RemoveAll(x => x.Id == id);
        return ValueTask.FromResult(true);
    }

    public ValueTask<IEnumerable<TenantConfiguration>> GetDeletedTenants(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IEnumerable<TenantConfiguration>>([]);

    public ValueTask<bool> RestoreTenant(string id, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    private static void Replace(List<TenantConfiguration> tenants, TenantConfiguration tenant)
    {
        var index = tenants.FindIndex(x => x.Id == tenant.Id);
        if (index >= 0)
            tenants[index] = tenant;
    }
}

internal sealed class RecordingChildService : IChildService
{
    public List<ChildConfiguration> Children { get; } = [];
    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public ValueTask<IEnumerable<ChildConfiguration>> GetChildren(string tenantId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<ChildConfiguration>>(Children.Where(x => x.TenantId == tenantId).ToArray());

    public ValueTask<IEnumerable<ChildWithBalance>> GetChildrenWithBalance(string tenantId, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<ChildWithBalance>>(Children
            .Where(x => x.TenantId == tenantId)
            .Select(x => new ChildWithBalance
            {
                Id = x.Id,
                TenantId = x.TenantId,
                Name = $"{x.FirstName} {x.LastName}",
                Balance = 10m,
                NextRegularChange = x.RegularAllowance,
                NextRegularChangeDate = DateTimeOffset.UtcNow.AddHours(4),
            }).ToArray());

    public ValueTask<ChildConfiguration> AddChild(ChildConfiguration child, CancellationToken cancellationToken)
    {
        AddCalls++;
        child.Id = child.Id == string.Empty ? Guid.NewGuid().ToString("N") : child.Id;
        Children.Add(child);
        return ValueTask.FromResult(child);
    }

    public ValueTask<ChildConfiguration> UpdateChild(ChildConfiguration child, CancellationToken cancellationToken)
    {
        UpdateCalls++;
        var index = Children.FindIndex(x => x.Id == child.Id);
        if (index >= 0)
            Children[index] = child;
        return ValueTask.FromResult(child);
    }

    public ValueTask<ChildConfiguration> ApplyHoldAsync(string childId, string tenantId, int days,
        string description, string? requestId, CancellationToken cancellationToken = default)
    {
        var child = Children.Single(x => x.Id == childId && x.TenantId == tenantId);
        child.HoldDaysRemaining = Math.Max(0, child.HoldDaysRemaining + days);
        return ValueTask.FromResult(child);
    }

    public ValueTask<ChildConfiguration> RemoveHoldDayAsync(string childId, string tenantId,
        string? requestId, CancellationToken cancellationToken = default)
    {
        var child = Children.Single(x => x.Id == childId && x.TenantId == tenantId);
        child.HoldDaysRemaining = Math.Max(0, child.HoldDaysRemaining - 1);
        return ValueTask.FromResult(child);
    }

    public ValueTask<bool> DeleteChild(string id, string tenantId, CancellationToken cancellationToken)
    {
        DeleteCalls++;
        Children.RemoveAll(x => x.Id == id && x.TenantId == tenantId);
        return ValueTask.FromResult(true);
    }

    public ValueTask<IEnumerable<ChildConfiguration>> GetDeletedChildren(string tenantId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IEnumerable<ChildConfiguration>>([]);

    public ValueTask<bool> RestoreChild(string id, string tenantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public ValueTask<ChildConfiguration?> GetChild(string childId, string childTenantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Children.SingleOrDefault(x => x.Id == childId && x.TenantId == childTenantId));

    public ValueTask<IEnumerable<ChildWithBalanceHistory>> GetChildrenWithBalanceHistory(
        string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<ChildWithBalanceHistory>>(Children
            .Where(x => x.TenantId == tenantId)
            .Select(x => new ChildWithBalanceHistory(x.Id, $"{x.FirstName} {x.LastName}", tenantId,
                [new BalanceHistoryEntry(DateTimeOffset.UtcNow.AddDays(-1), 1m), new BalanceHistoryEntry(DateTimeOffset.UtcNow, 10m)]))
            .ToArray());
}

internal sealed class RecordingTransactionService : ITransactionService
{
    public List<AllowanceTransaction> Transactions { get; } = [];
    public AllowanceTransaction? LastAdded { get; private set; }
    public int LastPage { get; private set; }
    public int LastPageSize { get; private set; }
    public bool LastIgnoreDailyAllowance { get; private set; }

    public ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(string childId, string tenantId,
        bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IEnumerable<AllowanceTransaction>>(Filter(childId, tenantId, ignoreDailyAllowance).ToArray());

    public ValueTask<PagedResult<AllowanceTransaction>> GetPagedTransactionsForChild(string childId, string tenantId,
        int page, int pageSize, bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default)
    {
        LastPage = page;
        LastPageSize = pageSize;
        LastIgnoreDailyAllowance = ignoreDailyAllowance;
        var items = Filter(childId, tenantId, ignoreDailyAllowance).ToArray();
        return ValueTask.FromResult(new PagedResult<AllowanceTransaction>(items, items.Length, page, pageSize));
    }

    public ValueTask<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Transactions.Where(x => x.ChildId == childId && x.TenantId == tenantId).LastOrDefault()?.Balance ?? 0m);

    public ValueTask<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default)
    {
        LastAdded = transaction;
        Transactions.Add(transaction);
        return ValueTask.FromResult(transaction);
    }

    public ValueTask<AllowanceTransaction?> GetLatestRegularTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Transactions.LastOrDefault(x => x.ChildId == childId && x.TenantId == tenantId && x.TransactionType == TransactionType.DailyAllowance));

    public ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Transactions.LastOrDefault(x => x.ChildId == childId && x.TenantId == tenantId));

    public ValueTask<AllowanceTransaction> ReverseTransactionAsync(string transactionId, string tenantId,
        string reason, string? requestId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<IEnumerable<BalanceHistoryEntry>> GetBalanceHistoryForChild(string childId, string tenantId,
        DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<BalanceHistoryEntry>>([]);

    private IEnumerable<AllowanceTransaction> Filter(string childId, string tenantId, bool ignoreDailyAllowance) =>
        Transactions.Where(x => x.ChildId == childId && x.TenantId == tenantId)
            .Where(x => !ignoreDailyAllowance || x.TransactionType != TransactionType.DailyAllowance);
}

internal sealed class RecordingUserService : IUserService
{
    public List<User> Users { get; } = [];
    public int AddToTenantCalls { get; private set; }

    public ValueTask<User> InitializeUserAsync(string email, string name, string? tenantId, CancellationToken cancellationToken)
    {
        var user = new User { Email = email, Name = name, Tenants = tenantId is null ? [] : [tenantId] };
        Users.Add(user);
        return ValueTask.FromResult(user);
    }

    public ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken)
    {
        var existing = Users.FindIndex(x => x.Id == user.Id);
        if (existing >= 0)
            Users[existing] = user;
        else
            Users.Add(user);
        return ValueTask.FromResult(user);
    }

    public ValueTask<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Users.SingleOrDefault(x => x.Email == email));

    public Task DeleteUserAsync(string email, CancellationToken cancellationToken)
    {
        Users.RemoveAll(x => x.Email == email);
        return Task.CompletedTask;
    }

    public ValueTask<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<User>>(Users.ToArray());

    public ValueTask<IEnumerable<User>> GetTenantUsersInRole(string tenantId, string role, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IEnumerable<User>>(Users.Where(x => x.Tenants.Contains(tenantId) && x.Roles.Contains(role)).ToArray());

    public ValueTask<bool> AddUserToTenantAsync(string email, string name, string tenantId, string role, CancellationToken cancellationToken)
    {
        AddToTenantCalls++;
        var user = Users.SingleOrDefault(x => x.Email == email) ?? new User { Email = email, Name = name };
        user.Name = name;
        user.Tenants = user.Tenants.Append(tenantId).Distinct().ToArray();
        user.Roles = user.Roles.Append(role).Distinct().ToArray();
        if (!Users.Contains(user))
            Users.Add(user);
        return ValueTask.FromResult(true);
    }
}

internal sealed class RecordingTenantNotificationService : ITenantNotificationService
{
    public event EventHandler<IGlobalNotificationService.ChildStateChangedEventArgs>? ChildStateChanged;

    public void OnChildStateChanged(string childId, string tenantId, string notificationMessage) =>
        ChildStateChanged?.Invoke(this, new IGlobalNotificationService.ChildStateChangedEventArgs
        {
            ChildId = childId,
            TenantId = tenantId,
            NotificationMessage = notificationMessage,
        });
}

internal sealed class RecordingCurrentContextService : ICurrentContextService
{
    public string? TenantId { get; private set; }
    public string? GetCurrentTenant() => TenantId;
    public void SetCurrentTenant(string tenantId) => TenantId = tenantId;
    public ValueTask<string?> GetCurrentTenantSuffix() => ValueTask.FromResult<string?>(null);
    public string? GetCurrentUserEmail() => null;
    public string GetCurrentUserName() => "Allowance schedule";
}

internal sealed class DelegatingScopeFactory(IServiceProvider provider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => new DelegatingScope(provider);

    private sealed class DelegatingScope(IServiceProvider provider) : IServiceScope, IAsyncDisposable
    {
        public IServiceProvider ServiceProvider => provider;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
