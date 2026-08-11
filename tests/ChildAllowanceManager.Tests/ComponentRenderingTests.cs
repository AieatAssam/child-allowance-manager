using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace ChildAllowanceManager.Tests;

public class ComponentRenderingTests
{
    [Fact]
    public async Task ChildEditorHidesBirthdayAllowanceUntilBirthDateIsSet()
    {
        await using var context = BunitTestContext.Create();
        var child = new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            RegularAllowance = 5m
        };

        var cut = context.Render<ChildConfigurationEditor>(parameters => parameters
            .Add(x => x.Child, child));

        Assert.Contains("First Name", cut.Markup);
        Assert.Contains("Regular Allowance", cut.Markup);
        Assert.DoesNotContain("Birthday Allowance", cut.Markup);

        child.BirthDate = new DateTime(2010, 5, 1);
        var birthdayCut = context.Render<ChildConfigurationEditor>(parameters => parameters
            .Add(x => x.Child, child));

        Assert.Contains("Birthday Allowance", birthdayCut.Markup);
    }

    [Fact]
    public async Task TenantEditorRendersTenantFieldsAndParentSection()
    {
        await using var context = BunitTestContext.Create();
        context.Services.Add(ServiceDescriptor.Singleton<IUserService>(new FakeUserService()));
        var tenant = new TenantConfiguration { TenantName = "Family", UrlSuffix = "family" };

        var cut = context.Render<TenantConfigurationEditor>(parameters => parameters
            .Add(x => x.Tenant, tenant));

        Assert.Contains("Name", cut.Markup);
        Assert.Contains("URL Suffix", cut.Markup);
        Assert.Contains("Time zone", cut.Markup);
        Assert.Contains("Parents", cut.Markup);
        Assert.Contains("Add parent", cut.Markup);
    }

    [Fact]
    public async Task TenantEditorHidesParentAssignmentControlsWhenReadOnly()
    {
        await using var context = BunitTestContext.Create();
        context.Services.Add(ServiceDescriptor.Singleton<IUserService>(new FakeUserService()));

        var cut = context.Render<TenantConfigurationEditor>(parameters => parameters
            .Add(x => x.Tenant, new TenantConfiguration { TenantName = "Family", UrlSuffix = "family" })
            .Add(x => x.ReadOnly, true));

        Assert.DoesNotContain("Parents", cut.Markup);
        Assert.DoesNotContain("Add parent", cut.Markup);
        Assert.Contains("Time zone", cut.Markup);
        Assert.All(cut.FindAll("input"), input =>
            Assert.True(input.HasAttribute("readonly") || input.HasAttribute("disabled")));
    }

    [Fact]
    public async Task ChildEditorHonorsReadOnlyMode()
    {
        await using var context = BunitTestContext.Create();
        var cut = context.Render<ChildConfigurationEditor>(parameters => parameters
            .Add(x => x.Child, new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace" })
            .Add(x => x.ReadOnly, true));

        Assert.DoesNotContain("Birthday Allowance", cut.Markup);
        Assert.All(cut.FindAll("input"), input => Assert.True(input.HasAttribute("readonly") || input.HasAttribute("disabled")));
    }

    [Fact]
    public async Task TransactionTableRendersServerRowsUsingOneBasedServicePages()
    {
        await using var context = BunitTestContext.Create();
        var transactions = new FakeTransactionService();
        context.Services.Add(ServiceDescriptor.Singleton<ITransactionService>(transactions));
        var child = new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Ada", Balance = 3m };

        var cut = context.Render<ChildTransactionsTable>(parameters => parameters
            .Add(x => x.Child, child));

        cut.WaitForAssertion(() => Assert.Contains("Pocket money", cut.Markup));
        Assert.Equal(1, transactions.LastPage);
        Assert.Equal(25, transactions.LastPageSize);
        Assert.False(transactions.LastIgnoreDailyAllowance);
        Assert.Contains("aria-label=\"Money in\"", cut.Markup);
        Assert.DoesNotContain("positive-amount", cut.Markup);
        Assert.DoesNotContain("negative-amount", cut.Markup);
    }

    [Fact]
    public async Task TransactionDialogueUsesActivityHeading()
    {
        await using var context = BunitTestContext.Create();
        context.Services.Add(ServiceDescriptor.Singleton<ITransactionService>(new FakeTransactionService()));
        var provider = context.Render<MudDialogProvider>();
        var parameters = new DialogParameters<ChildTransactionsDialogue>();
        parameters.Add(x => x.Child, new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Ada", Balance = 3m });

        await context.Services.GetRequiredService<IDialogService>()
            .ShowAsync<ChildTransactionsDialogue>(null, parameters);

        provider.WaitForAssertion(() => Assert.Contains("Ada’s activity", provider.Markup));
        Assert.DoesNotContain("Money trail", provider.Markup);
    }

    private sealed class FakeUserService : IUserService
    {
        public ValueTask<User> InitializeUserAsync(string email, string name, string? tenantId, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new User { Email = email, Name = name });

        public ValueTask<User> UpsertUserAsync(User user, CancellationToken cancellationToken) => ValueTask.FromResult(user);

        public ValueTask<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken) => ValueTask.FromResult<User?>(null);

        public Task DeleteUserAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask<IEnumerable<User>> GetUsersAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IEnumerable<User>>([]);

        public ValueTask<IEnumerable<User>> GetTenantUsersInRole(string tenantId, string role, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IEnumerable<User>>([]);

        public ValueTask<bool> AddUserToTenantAsync(string email, string name, string tenantId, string role, CancellationToken cancellationToken) =>
            ValueTask.FromResult(true);
    }

    private sealed class FakeTransactionService : ITransactionService
    {
        public int LastPage { get; private set; }
        public int LastPageSize { get; private set; }
        public bool LastIgnoreDailyAllowance { get; private set; }

        public ValueTask<IEnumerable<AllowanceTransaction>> GetTransactionsForChild(
            string childId, string tenantId, bool ignoreDailyAllowance = false,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IEnumerable<AllowanceTransaction>>([]);

        public ValueTask<PagedResult<AllowanceTransaction>> GetPagedTransactionsForChild(
            string childId, string tenantId, int page, int pageSize,
            bool ignoreDailyAllowance = false, CancellationToken cancellationToken = default)
        {
            LastPage = page;
            LastPageSize = pageSize;
            LastIgnoreDailyAllowance = ignoreDailyAllowance;
            var timestamp = DateTimeOffset.UtcNow;
            return ValueTask.FromResult(new PagedResult<AllowanceTransaction>(
                [new AllowanceTransaction
                {
                    ChildId = childId,
                    TenantId = tenantId,
                    TransactionAmount = 3m,
                    Balance = 3m,
                    Description = "Pocket money",
                    TransactionTimestamp = timestamp,
                    TransactionType = TransactionType.Deposit
                }], 1, page, pageSize));
        }

        public ValueTask<decimal> GetBalanceForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(3m);

        public ValueTask<AllowanceTransaction> AddTransaction(AllowanceTransaction transaction, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(transaction);

        public ValueTask<AllowanceTransaction?> GetLatestRegularTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AllowanceTransaction?>(null);

        public ValueTask<AllowanceTransaction?> GetLatestTransactionForChild(string childId, string tenantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AllowanceTransaction?>(null);

        public ValueTask<IEnumerable<BalanceHistoryEntry>> GetBalanceHistoryForChild(
            string childId, string tenantId, DateTimeOffset? startDate, DateTimeOffset? endDate,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IEnumerable<BalanceHistoryEntry>>([]);
    }
}
