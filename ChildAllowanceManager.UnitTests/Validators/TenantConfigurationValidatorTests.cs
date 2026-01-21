using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Validators;

[TestClass]
public class TenantConfigurationValidatorTests
{
    [TestMethod]
    public void Validator_FailsForShortName()
    {
        var validator = new TenantConfigurationValidator();
        var tenant = new TenantConfiguration
        {
            TenantName = "abc",
            UrlSuffix = "def"
        };

        var result = validator.Validate(tenant);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TenantConfiguration.TenantName));
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TenantConfiguration.UrlSuffix));
    }
}
