using System.Runtime.CompilerServices;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ChildConfigurationEditor : CancellableComponentBase
{
    [Parameter]
    public ChildConfiguration Child { get; set; }
    
    [Parameter]
    public EventCallback<ChildConfiguration> ChildChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;

    private MudForm _form;
    private ChildConfigurationValidator _validator = new();
    
    private async Task OnChildChanged()
    {
        await _form.Validate();
        if (_form.IsValid)
        {
            await ChildChanged.InvokeAsync(Child);
        }
    }

}