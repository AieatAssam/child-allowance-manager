using System.Runtime.CompilerServices;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;

namespace ChildAllowanceManager.Components;

public partial class ChildConfigurationEditor : ComponentBase
{
    [Parameter]
    public ChildConfiguration Child { get; set; }
    
    [Parameter]
    public EventCallback<ChildConfiguration> ChildChanged { get; set; }

    [Parameter] 
    public bool ReadOnly { get; set; } = false;
    
    private async Task OnChildChanged()
    {
        await ChildChanged.InvokeAsync(Child);
    }

}