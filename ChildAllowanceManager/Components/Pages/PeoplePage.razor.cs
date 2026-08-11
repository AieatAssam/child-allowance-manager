using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class PeoplePage : CancellableComponentBase
{
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IMembershipService MembershipService { get; set; } = default!;
    [Inject] private IInvitationService InvitationService { get; set; } = default!;
    [Inject] private ITenantAuthorizationService TenantAuthorization { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public string? TenantSuffix { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    private string? _tenantId;
    private TenantMembership[]? Members;
    private TenantInvitation[]? Invitations;
    private MudMessageBox Confirmation { get; set; } = null!;

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(TenantSuffix))
            return;

        TenantConfiguration? tenant = null;
        var outcome = await RunAsync(async () =>
            tenant = await TenantService.GetTenantBySuffix(TenantSuffix, CancellationToken));
        if (!outcome.Succeeded)
            return;

        if (tenant is null)
        {
            Navigation.NavigateTo("/error/404");
            return;
        }

        if (AuthenticationState is null ||
            !TenantAuthorization.CanManagePeople((await AuthenticationState).User, tenant.Id))
        {
            Navigation.NavigateTo("/");
            return;
        }

        _tenantId = tenant.Id;
        await ReloadAsync();
        await base.OnParametersSetAsync();
    }

    private async Task ReloadAsync()
    {
        if (_tenantId is null)
            return;

        TenantMembership[]? members = null;
        TenantInvitation[]? invitations = null;
        var outcome = await RunAsync(async () =>
        {
            members = (await MembershipService.GetMembershipsForTenantAsync(_tenantId, CancellationToken)).ToArray();
            invitations = (await InvitationService.GetPendingForTenantAsync(_tenantId, CancellationToken)).ToArray();
        });
        if (outcome.Succeeded)
        {
            Members = members;
            Invitations = invitations;
        }
    }

    private async Task InviteParent()
    {
        if (_tenantId is null)
            return;

        var parameters = new DialogParameters<AddParentDialogue>();
        parameters.Add(x => x.TenantId, _tenantId);
        var dialog = await DialogService.ShowAsync<AddParentDialogue>("Invite a parent", parameters);
        var result = await dialog.Result;
        if (!result.Canceled)
            await ReloadAsync();
    }

    private async Task RemoveMember(TenantMembership membership)
    {
        if (_tenantId is null || true != await Confirmation.ShowAsync())
            return;

        var outcome = await RunAsync(
            async () => await MembershipService.RevokeAsync(membership.UserId, _tenantId, CancellationToken),
            successMessage: "Person removed from this family.");
        if (outcome.Succeeded)
            await ReloadAsync();
    }

    private async Task WithdrawInvitation(TenantInvitation invitation)
    {
        if (_tenantId is null || true != await Confirmation.ShowAsync())
            return;

        var outcome = await RunAsync(
            async () => await InvitationService.RevokeAsync(invitation.Id, _tenantId, CancellationToken),
            successMessage: "Invitation withdrawn.");
        if (outcome.Succeeded)
            await ReloadAsync();
    }
}
