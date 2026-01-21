using System.Linq.Expressions;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Services;

[TestClass]
public class UserServiceTests
{
    [TestMethod]
    public async Task InitializeUserAsync_FirstUserGetsAdminRole()
    {
        var repo = new Mock<IRepository<User>>();
        repo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        repo.Setup(r => r.GetAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        repo.Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var service = new UserService(repo.Object, NullLogger<UserService>.Instance);

        var user = await service.InitializeUserAsync("first@example.com", "First User", "tenant-1", default);

        user.Roles.ShouldContain(ValidRoles.Admin);
        user.Tenants.ShouldContain("tenant-1");
    }

    [TestMethod]
    public async Task AddUserToTenantAsync_UpdatesExistingUser()
    {
        var repo = new Mock<IRepository<User>>();
        var existing = new User
        {
            Id = "user-1",
            Email = "user@example.com",
            Name = "Original",
            Roles = new[] { ValidRoles.Parent },
            Tenants = new[] { "tenant-1" }
        };

        repo.Setup(r => r.GetAsync(It.IsAny<Expression<Func<User, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });
        repo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, bool _, CancellationToken _) => u);

        var service = new UserService(repo.Object, NullLogger<UserService>.Instance);

        var result = await service.AddUserToTenantAsync("user@example.com", "Updated", "tenant-2", ValidRoles.Admin, default);

        result.ShouldBeTrue();
        existing.Tenants.ShouldContain("tenant-2");
        existing.Roles.ShouldContain(ValidRoles.Admin);
        existing.Name.ShouldBe("Updated");
    }
}
