using ChildAllowanceManager.Common.Models;
using FluentValidation;

namespace ChildAllowanceManager.Common.Validators;

public class TenantConfigurationValidator: AbstractValidator<TenantConfiguration>
{
    public TenantConfigurationValidator()
    {
        RuleFor(tenant => tenant.TenantName)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MinimumLength(5);
        RuleFor(tenant => tenant.UrlSuffix)
            .NotEmpty()
            .MinimumLength(5)
            .Matches("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")
            .WithMessage("URL suffix may contain lowercase letters, numbers, and hyphens only");
    }
    
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<TenantConfiguration>.CreateWithOptions((TenantConfiguration)model, x => x.IncludeProperties(propertyName)));
        if (result.IsValid)
            return Array.Empty<string>();
        return result.Errors.Select(e => e.ErrorMessage);
    };
}
