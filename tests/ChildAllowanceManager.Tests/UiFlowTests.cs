using Bunit;
using Bunit.TestDoubles;
using System.Security.Claims;
using ChildAllowanceManager;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Components;
using ChildAllowanceManager.Components.Layout;
using ChildAllowanceManager.Components.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace ChildAllowanceManager.Tests;

public class UiFlowTests
{
    [Fact]
    public async Task ChildManagementRendersChildrenAndEntersAddMode()
    {
        await using var context = BunitTestContext.Create();
        var tenantService = new RecordingTenantService();
        tenantService.Tenants.Add(new TenantConfiguration { Id = "tenant-1", TenantName = "Demo", UrlSuffix = "demo" });
        var childService = new RecordingChildService();
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Alex", LastName = "Demo", RegularAllowance = 5m
        });
        context.Services.AddSingleton<ITenantService>(tenantService);
        context.Services.AddSingleton<IChildService>(childService);

        var cut = context.Render<ChildManagementPage>(parameters => parameters.Add(x => x.TenantSuffix, "demo"));

        cut.WaitForAssertion(() => Assert.Contains("Alex Demo", cut.Markup));
        cut.Find("button[aria-label='Add child']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Add a child", cut.Markup));
        Assert.Contains("Save", cut.Markup);
        Assert.Contains("Clear", cut.Markup);
    }

    [Fact]
    public async Task ChildManagementEditFlowMakesFieldsWritableAndSavesChanges()
    {
        await using var context = BunitTestContext.Create();
        var tenantService = new RecordingTenantService();
        tenantService.Tenants.Add(new TenantConfiguration { Id = "tenant-1", TenantName = "Demo", UrlSuffix = "demo" });
        var childService = new RecordingChildService();
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Alex", LastName = "Demo", RegularAllowance = 5m
        });
        context.Services.AddSingleton<ITenantService>(tenantService);
        context.Services.AddSingleton<IChildService>(childService);

        var cut = context.Render<ChildManagementPage>(parameters => parameters.Add(x => x.TenantSuffix, "demo"));
        cut.WaitForAssertion(() => Assert.Contains("Edit", cut.Markup));
        cut.FindAll("button").Single(x => x.TextContent.Trim() == "Edit").Click();

        cut.WaitForAssertion(() => Assert.Contains("Cancel", cut.Markup));
        Assert.Contains("Save", cut.Markup);
        Assert.Contains("value=\"Alex\"", cut.Markup);
    }

    [Fact]
    public async Task AdministrationRendersTenantsAndEntersAddMode()
    {
        await using var context = BunitTestContext.Create();
        var tenantService = new RecordingTenantService();
        tenantService.Tenants.Add(new TenantConfiguration { Id = "tenant-1", TenantName = "Demo family", UrlSuffix = "demo" });
        context.Services.AddSingleton<ITenantService>(tenantService);
        context.Services.AddSingleton<IUserService>(new RecordingUserService());

        var cut = context.Render<AdministrationPage>();

        cut.WaitForAssertion(() => Assert.Contains("Demo family", cut.Markup));
        Assert.Contains("Families", cut.Markup);
        Assert.Contains("Open this family", cut.Markup);
        Assert.Contains("Copy the link to this family", cut.Markup);
        cut.Find("button[aria-label='Add family']").Click();

        cut.WaitForAssertion(() => Assert.Contains("Add a family", cut.Markup));
        Assert.Contains("Save", cut.Markup);
        Assert.Contains("Clear", cut.Markup);
    }

    [Fact]
    public async Task ConfigurationEditorsExposeConditionalAndReadOnlyControls()
    {
        await using var context = BunitTestContext.Create();
        context.Services.AddSingleton<IUserService>(new RecordingUserService());
        var child = new ChildConfiguration { FirstName = "Ada", LastName = "Lovelace" };

        var childCut = context.Render<ChildConfigurationEditor>(parameters => parameters
            .Add(x => x.Child, child));
        Assert.DoesNotContain("Birthday Allowance", childCut.Markup);
        child.BirthDate = new DateTime(2010, 1, 1);
        childCut.Render();
        Assert.Contains("Birthday Allowance", childCut.Markup);

        var readOnlyCut = context.Render<ChildConfigurationEditor>(parameters => parameters
            .Add(x => x.Child, child)
            .Add(x => x.ReadOnly, true));
        Assert.All(readOnlyCut.FindAll("input"), input =>
            Assert.True(input.HasAttribute("readonly") || input.HasAttribute("disabled")));

        var tenantCut = context.Render<TenantConfigurationEditor>(parameters => parameters
            .Add(x => x.Tenant, new TenantConfiguration { TenantName = "Demo", UrlSuffix = "demo" })
            .Add(x => x.ReadOnly, true));
        Assert.DoesNotContain("Parents", tenantCut.Markup);
        Assert.DoesNotContain("Add parent", tenantCut.Markup);
    }

    [Fact]
    public async Task TransactionTableLoadsRowsAndRequestsFirstPage()
    {
        await using var context = BunitTestContext.Create();
        var transactions = new RecordingTransactionService();
        transactions.Transactions.AddRange([
            new AllowanceTransaction
            {
                ChildId = "child-1", TenantId = "tenant-1", Description = "Daily allowance",
                TransactionAmount = 5m, Balance = 5m, TransactionType = TransactionType.DailyAllowance,
                TransactionTimestamp = DateTimeOffset.UtcNow
            },
            new AllowanceTransaction
            {
                ChildId = "child-1", TenantId = "tenant-1", Description = "Saved pocket money",
                TransactionAmount = 2m, Balance = 7m, TransactionType = TransactionType.Deposit,
                TransactionTimestamp = DateTimeOffset.UtcNow.AddHours(-1)
            }
        ]);
        context.Services.AddSingleton<ITransactionService>(transactions);
        var child = new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Alex", Balance = 7m };

        var cut = context.Render<ChildTransactionsTable>(parameters => parameters.Add(x => x.Child, child));
        cut.WaitForAssertion(() => Assert.Contains("Saved pocket money", cut.Markup));
        Assert.Contains("Daily allowance", cut.Markup);

        Assert.Equal(1, transactions.LastPage);
        Assert.Equal(25, transactions.LastPageSize);
        Assert.False(transactions.LastIgnoreDailyAllowance);
        Assert.Contains("Details", cut.Markup);
        Assert.Contains("aria-label=\"Money in\"", cut.Markup);
    }

    [Fact]
    public void NavigationMenuShowsTenantLinksForAuthorisedUsers()
    {
        using var context = BunitTestContext.Create();
        var auth = context.AddAuthorization();
        auth.SetAuthorized("Parent");
        auth.SetRoles("parent", "admin");
        var navigation = context.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/demo/children");

        var cut = context.Render<NavMenu>();

        Assert.Contains("/demo/children", cut.Markup);
        Assert.Contains("/demo/configuration", cut.Markup);
        Assert.Contains("/admin", cut.Markup);
        Assert.Contains("Children", cut.Markup);
        Assert.Contains("Family settings", cut.Markup);
        Assert.Contains("Administration", cut.Markup);
    }

    [Fact]
    public async Task ActionDialogsRenderRequiredFieldsAndActions()
    {
        await using var context = BunitTestContext.Create();
        context.Services.AddSingleton<ITransactionService>(new RecordingTransactionService());
        context.Services.AddSingleton<IChildService>(new RecordingChildService());
        context.Services.AddSingleton<IUserService>(new RecordingUserService());
        var child = new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Alex", Balance = 7m };
        var provider = context.Render<MudDialogProvider>();
        var dialogService = context.Services.GetRequiredService<IDialogService>();

        var addParameters = new DialogParameters<AddFundsDialog>();
        addParameters.Add(x => x.Child, child);
        var addReference = await dialogService.ShowAsync<AddFundsDialog>("Add Funds", addParameters);
        provider.WaitForAssertion(() => Assert.Contains("Amount", provider.Markup));
        Assert.Contains("Description", provider.Markup);
        Assert.Contains("Round up amount", provider.Markup);
        Assert.Contains("Add", provider.Markup);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        await addReference.Result;

        var withdrawParameters = new DialogParameters<WithdrawFundsDialog>();
        withdrawParameters.Add(x => x.Child, child);
        var withdrawReference = await dialogService.ShowAsync<WithdrawFundsDialog>("Withdraw Funds", withdrawParameters);
        provider.WaitForAssertion(() => Assert.Contains("Withdraw", provider.Markup));
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        await withdrawReference.Result;

        var holdParameters = new DialogParameters<AddHoldDialog>();
        holdParameters.Add(x => x.Child, child);
        var holdReference = await dialogService.ShowAsync<AddHoldDialog>("Suspend Allowance", holdParameters);
        provider.WaitForAssertion(() => Assert.Contains("Days", provider.Markup));
        Assert.Contains("Suspend", provider.Markup);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        await holdReference.Result;

        var parentParameters = new DialogParameters<AddParentDialog>();
        parentParameters.Add(x => x.TenantId, "tenant-1");
        var parentReference = await dialogService.ShowAsync<AddParentDialog>("Add Parent", parentParameters);
        provider.WaitForAssertion(() => Assert.Contains("Email", provider.Markup));
        Assert.Contains("Name (optional)", provider.Markup);
        Assert.Contains("Send invitation", provider.Markup);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Cancel").Click();
        await parentReference.Result;
    }

    [Fact]
    public async Task TransactionDialogShowsReadableHistoryAndCloseControl()
    {
        await using var context = BunitTestContext.Create();
        var transactions = new RecordingTransactionService();
        transactions.Transactions.Add(new AllowanceTransaction
        {
            ChildId = "child-1", TenantId = "tenant-1", Description = "Skate park",
            TransactionAmount = -3m, Balance = 4m, TransactionType = TransactionType.Withdrawal,
            TransactionTimestamp = DateTimeOffset.UtcNow
        });
        context.Services.AddSingleton<ITransactionService>(transactions);
        var child = new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Alex", Balance = 4m };
        var provider = context.Render<MudDialogProvider>();
        var parameters = new DialogParameters<ChildTransactionsDialog>();
        parameters.Add(x => x.Child, child);

        var reference = await context.Services.GetRequiredService<IDialogService>()
            .ShowAsync<ChildTransactionsDialog>(null, parameters);

        provider.WaitForAssertion(() => Assert.Contains("Skate park", provider.Markup));
        Assert.Contains("Current balance", provider.Markup);
        Assert.Contains("Hide daily allowances", provider.Markup);

        provider.Find("button[aria-label^='Close ']").Click();
        await reference.Result;
        Assert.DoesNotContain("Skate park", provider.Markup);
    }

    [Fact]
    public async Task AddFundsDialogRoundsUpAndPersistsTheDeposit()
    {
        await using var context = BunitTestContext.Create();
        var transactions = new RecordingTransactionService();
        context.Services.AddSingleton<ITransactionService>(transactions);
        var child = new ChildWithBalance { Id = "child-1", TenantId = "tenant-1", Name = "Alex", Balance = 4m };
        var provider = context.Render<MudDialogProvider>();
        var parameters = new DialogParameters<AddFundsDialog>();
        parameters.Add(x => x.Child, child);
        var reference = await context.Services.GetRequiredService<IDialogService>()
            .ShowAsync<AddFundsDialog>(null, parameters);

        provider.WaitForAssertion(() => Assert.Contains("Round up amount", provider.Markup));
        var inputs = provider.FindAll("input");
        inputs[0].Change("0.75");
        provider.Find("button[aria-label='Round up amount']").Click();
        Assert.Contains("£1.00", provider.Markup);
        inputs[1].Change("Saved for a goal");
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Add").Click();

        await reference.Result;
        Assert.NotNull(transactions.LastAdded);
        Assert.Equal(1m, transactions.LastAdded!.TransactionAmount);
        Assert.Equal("Saved for a goal", transactions.LastAdded.Description);
    }

    [Fact]
    public async Task DashboardRendersChildActionsAndAccessibleChart()
    {
        await using var context = BunitTestContext.Create();
        context.Services.AddDataProtection();
        context.Services.AddSingleton<ProtectedLocalStorage>(services =>
            new ProtectedLocalStorage(context.JSInterop.JSRuntime,
                services.GetRequiredService<IDataProtectionProvider>()));
        context.Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        var tenantService = new RecordingTenantService();
        tenantService.Tenants.Add(new TenantConfiguration { Id = "tenant-1", TenantName = "Demo", UrlSuffix = "demo" });
        var childService = new RecordingChildService();
        childService.Children.Add(new ChildConfiguration
        {
            Id = "child-1", TenantId = "tenant-1", FirstName = "Alex", LastName = "Demo", RegularAllowance = 5m
        });
        context.Services.AddSingleton<ITenantService>(tenantService);
        context.Services.AddSingleton<IChildService>(childService);
        context.Services.AddSingleton<ITenantNotificationService>(new RecordingTenantNotificationService());
        context.Services.AddSingleton<ITransactionService>(new RecordingTransactionService());
        context.Services.AddSingleton<ICurrentContextService>(new RecordingCurrentContextService());
        var auth = context.AddAuthorization();
        auth.SetAuthorized("Parent");
        auth.SetRoles("parent");
        auth.SetClaims(new Claim(CustomClaimTypes.Tenant, "tenant-1"));

        var cut = context.Render<ChildrenListPage>(parameters => parameters
            .Add(x => x.TenantSuffix, "demo")
            .AddCascadingValue(new ThemeConfiguration()));

        cut.WaitForAssertion(() => Assert.Contains("Alex Demo", cut.Markup));
        Assert.Contains("Balances", cut.Markup);
        var expectedDate = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))
            .ToString("dddd, d MMMM");
        Assert.Contains(expectedDate, cut.Markup);
        Assert.Contains("Next allowance", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("Add money", cut.Markup);
        Assert.Contains("Withdraw", cut.Markup);
        Assert.DoesNotContain("Today at a glance", cut.Markup);
        Assert.Contains("More actions", cut.Markup);
        Assert.Contains("Balance over time", cut.Markup);
        Assert.Contains("Tap or hover a point", cut.Markup);
    }
}
