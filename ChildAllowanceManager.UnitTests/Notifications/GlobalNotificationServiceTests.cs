using ChildAllowanceManager.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Notifications;

[TestClass]
public class GlobalNotificationServiceTests
{
    [TestMethod]
    public void OnChildStateChanged_RaisesEventWithArguments()
    {
        var service = new GlobalNotificationService();
        string? tenantId = null;
        string? childId = null;
        string? message = null;

        service.ChildStateChanged += (_, args) =>
        {
            tenantId = args.TenantId;
            childId = args.ChildId;
            message = args.NotificationMessage;
        };

        service.OnChildStateChanged("child-1", "tenant-1", "updated");

        tenantId.ShouldBe("tenant-1");
        childId.ShouldBe("child-1");
        message.ShouldBe("updated");
    }
}
