using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Specification;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Services;

[TestClass]
public class TransactionServiceTests
{
    [TestMethod]
    public async Task GetBalanceForChild_ReturnsLatestBalance()
    {
        var repo = new Mock<IRepository<AllowanceTransaction>>();
        var notifications = new Mock<IGlobalNotificationService>();

        var queryResult = new Mock<IQueryResult<AllowanceTransaction>>();
        queryResult.SetupGet(p => p.Items).Returns(new[]
        {
            new AllowanceTransaction { Balance = 12m, TransactionTimestamp = DateTimeOffset.UtcNow },
            new AllowanceTransaction { Balance = 9m, TransactionTimestamp = DateTimeOffset.UtcNow.AddDays(-1) }
        });

        repo.Setup(r => r.QueryAsync(It.IsAny<ISpecification<AllowanceTransaction, IQueryResult<AllowanceTransaction>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult.Object);

        var service = new TransactionService(repo.Object, notifications.Object, NullLogger<TransactionService>.Instance);

        var balance = await service.GetBalanceForChild("child-1", "tenant-1", default);

        balance.ShouldBe(12m);
    }

    [TestMethod]
    public async Task GetBalanceHistoryForChild_FillsMissingDays()
    {
        var repo = new Mock<IRepository<AllowanceTransaction>>();
        var notifications = new Mock<IGlobalNotificationService>();

        var start = DateTimeOffset.UtcNow.Date.AddDays(-2);
        var middle = start.AddDays(1);
        var end = start.AddDays(2);

        var queryResult = new Mock<IQueryResult<AllowanceTransaction>>();
        queryResult.SetupGet(p => p.Items).Returns(new[]
        {
            new AllowanceTransaction { TransactionTimestamp = start, Balance = 3m },
            new AllowanceTransaction { TransactionTimestamp = middle, Balance = 6m }
        });

        repo.Setup(r => r.QueryAsync(It.IsAny<ISpecification<AllowanceTransaction, IQueryResult<AllowanceTransaction>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryResult.Object);

        var service = new TransactionService(repo.Object, notifications.Object, NullLogger<TransactionService>.Instance);

        var result = (await service.GetBalanceHistoryForChild("child-1", "tenant-1", start, end, default)).ToList();

        result.Count.ShouldBe(3);
        result[0].Balance.ShouldBe(3m);
        result[1].Balance.ShouldBe(6m);
        result[2].Balance.ShouldBe(6m);
    }
}
