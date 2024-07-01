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

    private string? _tenantId { get; set; }

    [Parameter]
    public string? TenantSuffix { get; set; }
    
    private ChildConfiguration[]? Children { get; set; } = null;

    private ChildConfiguration NewChild { get; set; } = new ChildConfiguration();
    private MudMessageBox DeleteChildMessageBox { get; set; } = null!;
    private bool AddingChild = false;
    private string? ChildBeingEditedId = null;

    protected override async Task OnInitializedAsync()
    {
        await ReloadChildren();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(TenantSuffix))
        {
            var tenant = await DataService.GetTenantBySuffix(TenantSuffix);
            if (tenant is null)
            {
                Navigation.NavigateTo("/error/404");
                return;
            }

            _tenantId = tenant.Id;
            await ReloadChildren();
        }
        await base.OnParametersSetAsync();
    }

    private async Task AddChild()
    {
        if (_tenantId is null)
        {
            return;
        }
        NewChild.TenantId = _tenantId;
        await DataService.AddChild(NewChild, CancellationToken.None);
        await ReloadChildren();
    }

    private async Task DeleteChild(ChildConfiguration child)
    {
        if (_tenantId is null)
        {
            return;
        }
        // Confirmation dialog
        if (true != await DeleteChildMessageBox.ShowAsync())
        {
            return;
        }
        await DataService.DeleteChild(child.Id, _tenantId, CancellationToken.None);
        await ReloadChildren();
    }

    private async Task ReloadChildren()
    {
        if (_tenantId is null)
        {
            return;
        }
        Children = (await DataService.GetChildren(_tenantId, CancellationToken.None)).ToArray();
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