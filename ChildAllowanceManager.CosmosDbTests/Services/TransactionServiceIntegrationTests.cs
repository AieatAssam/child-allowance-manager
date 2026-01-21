using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.CosmosDbTests.Fixtures;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.CosmosDbTests.Services;

[TestClass]
public class TransactionServiceIntegrationTests
{
    private static readonly CosmosDbFixture Fixture = new();

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        await Fixture.InitializeAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        await Fixture.DisposeAsync();
    }

    [TestMethod]
    public async Task AddTransaction_ComputesBalanceAndPersists()
    {
        if (!Fixture.IsAvailable)
        {
            Assert.Inconclusive(Fixture.SkipReason ?? "Docker is unavailable for Cosmos DB tests.");
        }

        await Fixture.ResetAsync();
        var notificationMock = new Mock<IGlobalNotificationService>();
        var repository = Fixture.GetRepository<AllowanceTransaction>();
        var service = new TransactionService(repository, notificationMock.Object, NullLogger<TransactionService>.Instance);

        var first = await service.AddTransaction(new AllowanceTransaction
        {
            ChildId = "child-1",
            TenantId = "tenant-1",
            TransactionAmount = 10m,
            TransactionType = TransactionType.DailyAllowance,
            Description = "Daily allowance"
        });

        var second = await service.AddTransaction(new AllowanceTransaction
        {
            ChildId = "child-1",
            TenantId = "tenant-1",
            TransactionAmount = -3m,
            TransactionType = TransactionType.Withdrawal,
            Description = "Purchase"
        });

        var transactions = await service.GetTransactionsForChild("child-1", "tenant-1");
        transactions.Count().ShouldBe(2);

        first.Balance.ShouldBe(10m);
        second.Balance.ShouldBe(7m);
        transactions.First().Id.ShouldBe(second.Id);
        transactions.Last().Id.ShouldBe(first.Id);

        notificationMock.Verify(n => n.OnChildStateChanged("child-1", "tenant-1",
            It.Is<string>(s => s.Contains("Balance changed"))), Times.Exactly(2));
    }

    [TestMethod]
    public async Task GetBalanceHistoryForChild_FillsMissingDays()
    {
        if (!Fixture.IsAvailable)
        {
            Assert.Inconclusive(Fixture.SkipReason ?? "Docker is unavailable for Cosmos DB tests.");
        }

        await Fixture.ResetAsync();
        var repository = Fixture.GetRepository<AllowanceTransaction>();
        var notificationMock = new Mock<IGlobalNotificationService>();
        var service = new TransactionService(repository, notificationMock.Object, NullLogger<TransactionService>.Instance);

        var startDate = DateTimeOffset.UtcNow.Date.AddDays(-2);
        var middleDate = startDate.AddDays(1);

        await repository.CreateAsync(new AllowanceTransaction
        {
            ChildId = "child-1",
            TenantId = "tenant-1",
            Balance = 5m,
            TransactionAmount = 5m,
            TransactionType = TransactionType.DailyAllowance,
            Description = "Start",
            TransactionTimestamp = startDate
        });

        await repository.CreateAsync(new AllowanceTransaction
        {
            ChildId = "child-1",
            TenantId = "tenant-1",
            Balance = 8m,
            TransactionAmount = 3m,
            TransactionType = TransactionType.DailyAllowance,
            Description = "Middle",
            TransactionTimestamp = middleDate
        });

        var history = (await service.GetBalanceHistoryForChild("child-1", "tenant-1",
            startDate, startDate.AddDays(2), CancellationToken.None)).ToList();

        history.Count.ShouldBe(3);
        history[0].Balance.ShouldBe(5m);
        history[1].Balance.ShouldBe(8m);
        history[2].Balance.ShouldBe(8m);
    }
}
