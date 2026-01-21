using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace ChildAllowanceManager.UnitTests.Validators;

[TestClass]
public class ChildConfigurationValidatorTests
{
    [TestMethod]
    public void Validator_FailsForFutureBirthDate()
    {
        var validator = new ChildConfigurationValidator();
        var child = new ChildConfiguration
        {
            FirstName = "Sam",
            LastName = "Smith",
            BirthDate = DateTime.Today.AddDays(1),
            RegularAllowance = 1m
        };

        var result = validator.Validate(child);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("future"));
    }
}
