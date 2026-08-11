using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildManagementPage : CancellableComponentBase
{
    [Inject] 
    private ITenantService TenantService { get; set; } = default!;
    
    [Inject]
    private IChildService ChildService { get; set; } = default!;
    
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;
    
    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private ITenantAuthorizationService TenantAuthorization { get; set; } = default!;

    private string? _tenantId { get; set; }

    [Parameter]
    public string? TenantSuffix { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }
    
    private ChildConfiguration[]? Children { get; set; } = null;
    private ChildConfiguration[]? DeletedChildren { get; set; }

    private ChildConfiguration NewChild { get; set; } = new ChildConfiguration();
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
            TenantConfiguration? tenant = null;
            var outcome = await RunAsync(
                async () => tenant = await TenantService.GetTenantBySuffix(TenantSuffix, CancellationToken));
            if (!outcome.Succeeded)
                return;

            if (tenant is null)
            {
                Navigation.NavigateTo("/error/404");
                return;
            }

            if (AuthenticationState is not null)
            {
                var user = (await AuthenticationState).User;
                if (!TenantAuthorization.CanManage(user, tenant.Id))
                {
                    Navigation.NavigateTo("/");
                    return;
                }
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
        var outcome = await RunAsync(
            async () => await ChildService.AddChild(NewChild, CancellationToken),
            successMessage: "Child added.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }

    private async Task DeleteChild(ChildConfiguration child)
    {
        if (_tenantId is null)
        {
            return;
        }
        if (!await ConfirmDialog.ShowAsync(
                DialogService,
                "Warning",
                "Delete this child? This hides the child and their history. A parent can restore them."))
        {
            return;
        }
        var outcome = await RunAsync(
            async () => await ChildService.DeleteChild(child.Id, _tenantId, CancellationToken),
            successMessage: "Child removed. A parent can restore them.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }

    private async Task RestoreChild(ChildConfiguration child)
    {
        if (_tenantId is null)
        {
            return;
        }
        var outcome = await RunAsync(
            async () => await ChildService.RestoreChild(child.Id, _tenantId, CancellationToken),
            successMessage: $"{child.FirstName} {child.LastName} restored.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }

    private async Task ReloadChildren()
    {
        if (_tenantId is null)
        {
            return;
        }
        ChildConfiguration[]? loaded = null;
        ChildConfiguration[]? deleted = null;
        var outcome = await RunAsync(
            async () =>
            {
                loaded = (await ChildService.GetChildren(_tenantId, CancellationToken)).ToArray();
                deleted = (await ChildService.GetDeletedChildren(_tenantId, CancellationToken)).ToArray();
            });
        if (!outcome.Succeeded)
            return;

        Children = loaded!;
        DeletedChildren = deleted!;
        ChildBeingEditedId = null;
        AddingChild = false;
        NewChild = new ChildConfiguration();
    }

    private async Task UpdateChild(ChildConfiguration child)
    {
        var outcome = await RunAsync(
            async () => await ChildService.UpdateChild(child, CancellationToken),
            successMessage: "Child updated.");
        if (outcome.Succeeded)
            await ReloadChildren();
    }
}
