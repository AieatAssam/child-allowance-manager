using System.Linq.Expressions;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Services;

[TestClass]
public class ChildServiceTests
{
    [TestMethod]
    public async Task GetChildrenWithBalance_ComputesNextRegularChangeWithHold()
    {
        var repoMock = new Mock<IRepository<ChildConfiguration>>();
        var notifications = new Mock<IGlobalNotificationService>();
        var transactionService = new Mock<ITransactionService>();

        var child = new ChildConfiguration
        {
            Id = "child-1",
            TenantId = "tenant-1",
            FirstName = "Sam",
            LastName = "Smith",
            RegularAllowance = 2m,
            HoldDaysRemaining = 2,
            BirthDate = DateTime.UtcNow.Date
        };

        repoMock.Setup(r => r.GetAsync(It.IsAny<Expression<Func<ChildConfiguration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { child });

        transactionService.Setup(t => t.GetLatestTransactionForChild("child-1", "tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllowanceTransaction { Balance = 5m });
        transactionService.Setup(t => t.GetLatestRegularTransactionForChild("child-1", "tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AllowanceTransaction { TransactionTimestamp = DateTimeOffset.UtcNow.AddDays(-1) });

        var service = new ChildService(new HttpClient(), repoMock.Object, notifications.Object,
            transactionService.Object, NullLogger<ChildService>.Instance);

        var result = (await service.GetChildrenWithBalance("tenant-1", default)).ToList();

        result.Count.ShouldBe(1);
        result[0].Balance.ShouldBe(5m);
        result[0].NextRegularChange.ShouldBe(child.BirthdayAllowance ?? child.RegularAllowance);
        result[0].NextRegularChangeDate.Date.ShouldBe(DateTimeOffset.UtcNow.AddDays(child.HoldDaysRemaining).Date);
        result[0].IsBirthday.ShouldBeTrue();
    }

    [TestMethod]
    public async Task DeleteChild_ReturnsFalseWhenMissing()
    {
        var repoMock = new Mock<IRepository<ChildConfiguration>>();
        var notifications = new Mock<IGlobalNotificationService>();
        var transactionService = new Mock<ITransactionService>();

        repoMock.Setup(r => r.TryGetAsync("child-1", "tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChildConfiguration?)null);

        var service = new ChildService(new HttpClient(), repoMock.Object, notifications.Object,
            transactionService.Object, NullLogger<ChildService>.Instance);

        var result = await service.DeleteChild("child-1", "tenant-1", default);

        result.ShouldBeFalse();
    }
}
