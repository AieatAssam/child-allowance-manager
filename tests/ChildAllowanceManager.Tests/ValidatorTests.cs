using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;

namespace ChildAllowanceManager.Tests;

public class ValidatorTests
{
    [Fact]
    public async Task ChildValidatorRejectsFutureBirthDateAndNonPositiveAllowance()
    {
        var result = await new ChildConfigurationValidator().ValidateAsync(new ChildConfiguration
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            BirthDate = DateTime.Today.AddDays(1),
            RegularAllowance = 0
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("future"));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ChildConfiguration.RegularAllowance));
    }

    [Fact]
    public async Task TenantValidatorRequiresUsefulNamesAndSuffixes()
    {
        var result = await new TenantConfigurationValidator().ValidateAsync(new TenantConfiguration
        {
            TenantName = "Home",
            UrlSuffix = "x"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TenantConfiguration.TenantName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(TenantConfiguration.UrlSuffix));
    }

    [Fact]
    public async Task TenantValidatorRequiresAnExistingTimeZone()
    {
        var validator = new TenantConfigurationValidator();
        var invalid = await validator.ValidateAsync(new TenantConfiguration { TenantName = "Family", TimeZoneId = "Not/AZone" });
        var valid = await validator.ValidateAsync(new TenantConfiguration { TenantName = "Family", TimeZoneId = "Europe/London" });

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.ErrorMessage == "Choose a valid time zone.");
        Assert.True(valid.IsValid);
    }
}
