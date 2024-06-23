using System.Runtime.CompilerServices;
using ChildAllowanceManager.Common.Models;
using ChildAllowanceManager.Common.Validators;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ChildConfigurationEditor : ComponentBase
{
    [Parameter]
    public ChildConfiguration Child { get; set; }
    
    [Parameter]
    public EventCallback<ChildConfiguration> ChildChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;

    private MudForm Form;
    private ChildConfigurationValidator Validator = new();
    
    private async Task OnChildChanged()
    {
        await Form.Validate();
        if (Form.IsValid)
        {
            await ChildChanged.InvokeAsync(Child);
        }
    }

}