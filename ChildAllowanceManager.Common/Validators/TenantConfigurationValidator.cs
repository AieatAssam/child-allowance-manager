using ChildAllowanceManager.Common.Models;
using FluentValidation;

namespace ChildAllowanceManager.Common.Validators;

public class TenantConfigurationValidator: AbstractValidator<TenantConfiguration>
{
    public TenantConfigurationValidator()
    {
        RuleFor(tenant => tenant.TenantName)
            .NotEmpty()
            .MinimumLength(5);
        RuleFor(tenant => tenant.UrlSuffix)
            .NotEmpty()
            .MinimumLength(5);
    }
    
    public Func<object, string, Task<IEnumerable<string>>> ValidateValue => async (model, propertyName) =>
    {
        var result = await ValidateAsync(ValidationContext<TenantConfiguration>.CreateWithOptions((TenantConfiguration)model, x => x.IncludeProperties(propertyName)));
        if (result.IsValid)
            return Array.Empty<string>();
        return result.Errors.Select(e => e.ErrorMessage);
    };
}