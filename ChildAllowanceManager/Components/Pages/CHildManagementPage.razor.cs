using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildManagementPage : ComponentBase
{
    [Inject]
    private IDataService DataService { get; set; } = default!;
    
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Parameter] public Guid TenantId { get; set; } = Guid.Empty;
    
    
    private ChildConfiguration[]? Children { get; set; } = null;

    private ChildConfiguration NewChild { get; set; } = new ChildConfiguration();
    private MudMessageBox DeleteChildMessageBox { get; set; } = null!;
    private bool AddingChild = false;
    private string? ChildBeingEditedId = null;

    protected override async Task OnInitializedAsync()
    {
        await ReloadChildren();
    }

    private async Task AddChild()
    {
        NewChild.TenantId = TenantId;
        await DataService.AddChild(NewChild, CancellationToken.None);
        await ReloadChildren();
    }

    private async Task DeleteChild(ChildConfiguration child)
    {
        // Confirmation dialog
        if (true != await DeleteChildMessageBox.Show())
        {
            return;
        }
        await DataService.DeleteChild(child.Id, TenantId, CancellationToken.None);
        await ReloadChildren();
    }

    private async Task ReloadChildren()
    {
        Children = (await DataService.GetChildren(TenantId, CancellationToken.None)).ToArray();
        ChildBeingEditedId = null;
        AddingChild = false;
        NewChild = new ChildConfiguration();
    }

    private async Task UpdateChild(ChildConfiguration child)
    {
        await DataService.UpdateChild(child, CancellationToken.None);
        await ReloadChildren();
    }
}