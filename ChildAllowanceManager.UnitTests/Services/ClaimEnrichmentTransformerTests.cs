using System.Security.Claims;
using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Services;

[TestClass]
public class ClaimEnrichmentTransformerTests
{
    [TestMethod]
    public async Task TransformAsync_AddsMissingRoles()
    {
        var userService = new Mock<IUserService>();
        userService.Setup(u => u.GetUserByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User
            {
                Email = "user@example.com",
                Roles = new[] { ValidRoles.Admin, ValidRoles.Parent }
            });

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Email, "user@example.com"),
            new Claim(ClaimTypes.Role, ValidRoles.Parent)
        }, "mock");
        var principal = new ClaimsPrincipal(identity);

        var transformer = new ClaimEnrichmentTransformer(userService.Object,
            NullLogger<ClaimEnrichmentTransformer>.Instance);

        var result = await transformer.TransformAsync(principal);

        result.Claims.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == ValidRoles.Admin);
        result.Claims.Count(c => c.Type == ClaimTypes.Role && c.Value == ValidRoles.Parent).ShouldBe(1);
    }
}
