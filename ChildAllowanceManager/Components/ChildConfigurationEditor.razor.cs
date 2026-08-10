using System.ComponentModel.DataAnnotations;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ChildConfigurationEditor : CancellableComponentBase
{
    [Parameter, Required]
    public ChildConfiguration Child { get; set; } = default!;
    
    [Parameter]
    public EventCallback<ChildConfiguration> ChildChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;

    private MudForm _form = default!;
    private ChildConfigurationValidator _validator = new();
    
    private async Task OnChildChanged()
    {
        await _form.ValidateAsync();
        if (_form.IsValid)
        {
            await ChildChanged.InvokeAsync(Child);
        }
    }

}
