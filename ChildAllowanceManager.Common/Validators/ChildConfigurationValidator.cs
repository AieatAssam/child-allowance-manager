
using ChildAllowanceManager.Common.Models;
using FluentValidation;
using FluentValidation.Results;

namespace ChildAllowanceManager.Common.Validators;

public class ChildConfigurationValidator : AbstractValidator<ChildConfiguration>
{
    public ChildConfigurationValidator()
    {
        RuleFor(child => child.BirthDate)
            .LessThanOrEqualTo(DateTime.Today)
            .When(child => child.BirthDate is not null)
            .WithMessage("Date of birth cannot be in the future");
        RuleFor(child => child.FirstName)
            .NotEmpty();
        RuleFor(child => child.LastName)
            .NotEmpty();
        RuleFor(child => child.RegularAllowance)
            .GreaterThan(0m);
        RuleFor(child => child.BirthdayAllowance)
            .GreaterThanOrEqualTo(0)
            .When(child => child.BirthDate is not null);

    }
    
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<ChildConfiguration>.CreateWithOptions((ChildConfiguration)model, x => x.IncludeProperties(propertyName)));
        if (result.IsValid)
            return Array.Empty<string>();
        return result.Errors.Select(e => e.ErrorMessage);
    };
}