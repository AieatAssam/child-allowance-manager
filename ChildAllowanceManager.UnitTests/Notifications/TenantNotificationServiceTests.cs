using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Notifications;

[TestClass]
public class TenantNotificationServiceTests
{
    [TestMethod]
    public void OnChildStateChanged_ShouldOnlyRaiseForCurrentTenant()
    {
        var currentContext = new Mock<ICurrentContextService>();
        currentContext.Setup(c => c.GetCurrentTenant()).Returns("tenant-1");
        var globalNotifications = new GlobalNotificationService();

        var handlerInvoked = false;
        using var service = new TenantNotificationService(currentContext.Object, globalNotifications,
            NullLogger<TenantNotificationService>.Instance);

        service.ChildStateChanged += (_, args) =>
        {
            handlerInvoked = true;
            args.TenantId.ShouldBe("tenant-1");
            args.ChildId.ShouldBe("child-1");
        };

        globalNotifications.OnChildStateChanged("child-1", "tenant-1", "message");
        handlerInvoked.ShouldBeTrue();

        handlerInvoked = false;
        globalNotifications.OnChildStateChanged("child-1", "tenant-2", "message");
        handlerInvoked.ShouldBeFalse();
    }
}
