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
public class TenantServiceTests
{
    [TestMethod]
    public async Task DeleteTenant_ShouldCascadeToChildrenAndUsers()
    {
        var tenantRepo = new Mock<IRepository<TenantConfiguration>>();
        var userRepo = new Mock<IRepository<User>>();
        var childService = new Mock<IChildService>();

        var tenant = new TenantConfiguration { Id = "tenant-1" };
        tenantRepo.Setup(r => r.TryGetAsync("tenant-1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        childService.Setup(c => c.GetChildren("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChildConfiguration { Id = "child-1", TenantId = "tenant-1" } });

        userRepo.Setup(r => r.GetAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new User { Id = "user-1", Tenants = new[] { "tenant-1" } }
            });

        var service = new TenantService(tenantRepo.Object, userRepo.Object, childService.Object,
            NullLogger<TenantService>.Instance);

        var result = await service.DeleteTenant("tenant-1", default);

        result.ShouldBeTrue();
        tenant.Deleted.ShouldBeTrue();
        childService.Verify(c => c.DeleteChild("child-1", "tenant-1", It.IsAny<CancellationToken>()), Times.Once);
        userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => !u.Tenants.Contains("tenant-1")), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        tenantRepo.Verify(r => r.UpdateAsync(It.Is<TenantConfiguration>(t => t.Deleted), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
