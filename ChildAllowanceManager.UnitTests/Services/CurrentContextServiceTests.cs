using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Services;

[TestClass]
public class CurrentContextServiceTests
{
    [TestMethod]
    public async Task GetCurrentTenantSuffix_ReturnsSuffixForCurrentTenant()
    {
        var tenantService = new Mock<ITenantService>();
        tenantService.Setup(t => t.GetTenant("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantConfiguration { Id = "tenant-1", UrlSuffix = "house" });

        var service = new CurrentContextService(tenantService.Object);
        service.SetCurrentTenant("tenant-1");

        var suffix = await service.GetCurrentTenantSuffix();

        suffix.ShouldBe("house");
    }
}
