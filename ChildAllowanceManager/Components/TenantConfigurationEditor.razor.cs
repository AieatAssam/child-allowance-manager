using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class TenantConfigurationEditor : ComponentBase
{
    [Parameter]
    public TenantConfiguration Tenant { get; set; }
    
    [Parameter]
    public EventCallback<TenantConfiguration> TenantChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;
    
    public TenantConfigurationValidator Validator = new();
    
    private MudForm Form;
    
    private async Task OnTenantChanged()
    {
        await Form.Validate();
        if (Form.IsValid)
        {
            await TenantChanged.InvokeAsync(Tenant);
        }
    }
}