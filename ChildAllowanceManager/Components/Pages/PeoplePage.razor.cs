using ChildAllowanceManager.Common.Interfaces;
using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class PeoplePage : CancellableComponentBase
{
    [Inject] private ITenantService TenantService { get; set; } = default!;
    [Inject] private IMembershipService MembershipService { get; set; } = default!;
    [Inject] private IInvitationService InvitationService { get; set; } = default!;
    [Inject] private IShareLinkService ShareLinkService { get; set; } = default!;
    [Inject] private ITenantAuthorizationService TenantAuthorization { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public string? TenantSuffix { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationState { get; set; }

    private string? _tenantId;
    private TenantMembership[]? Members;
    private TenantInvitation[]? Invitations;
    private ShareLink[]? ShareLinks;
    private string? LoadError;
    private MudMessageBox Confirmation { get; set; } = null!;
    private string? _confirmationTitle;
    private string? _confirmationMessage;

    protected override async Task OnParametersSetAsync()
    {
        if (string.IsNullOrWhiteSpace(TenantSuffix))
            return;

        TenantConfiguration? tenant = null;
        TenantMembership[]? members = null;
        TenantInvitation[]? invitations = null;
        ShareLink[]? shareLinks = null;
        var canManage = false;
        LoadError = null;
        var outcome = await RunAsync(async () =>
        {
            tenant = await TenantService.GetTenantBySuffix(TenantSuffix, CancellationToken);
            if (tenant is null || AuthenticationState is null)
                return;

            canManage = TenantAuthorization.CanManagePeople((await AuthenticationState).User, tenant.Id);
            if (!canManage)
                return;

            members = (await MembershipService.GetMembershipsForTenantAsync(tenant.Id, CancellationToken)).ToArray();
            invitations = (await InvitationService.GetPendingForTenantAsync(tenant.Id, CancellationToken)).ToArray();
            shareLinks = (await ShareLinkService.GetForTenantAsync(tenant.Id, CancellationToken)).ToArray();
        });
        if (!outcome.Succeeded)
        {
            LoadError = outcome.ErrorMessage ?? "Unable to load family access.";
            StateHasChanged();
            return;
        }

        if (tenant is null)
        {
            Navigation.NavigateTo("/error/404");
            return;
        }

        if (!canManage)
        {
            Navigation.NavigateTo("/");
            return;
        }

        _tenantId = tenant.Id;
        Members = members;
        Invitations = invitations;
        ShareLinks = shareLinks;
        await base.OnParametersSetAsync();
    }

    private async Task ReloadAsync()
    {
        if (_tenantId is null)
            return;

        TenantMembership[]? members = null;
        TenantInvitation[]? invitations = null;
        ShareLink[]? shareLinks = null;
        LoadError = null;
        var outcome = await RunAsync(async () =>
        {
            members = (await MembershipService.GetMembershipsForTenantAsync(_tenantId, CancellationToken)).ToArray();
            invitations = (await InvitationService.GetPendingForTenantAsync(_tenantId, CancellationToken)).ToArray();
            shareLinks = (await ShareLinkService.GetForTenantAsync(_tenantId, CancellationToken)).ToArray();
        });
        if (outcome.Succeeded)
        {
            Members = members;
            Invitations = invitations;
            ShareLinks = shareLinks;
        }
        else
        {
            LoadError = outcome.ErrorMessage ?? "Unable to load family access.";
            StateHasChanged();
        }
    }

    private async Task InviteParent()
    {
        if (_tenantId is null)
            return;

        var parameters = new DialogParameters<AddParentDialog>();
        parameters.Add(x => x.TenantId, _tenantId);
        var dialog = await DialogService.ShowAsync<AddParentDialog>("Invite a parent", parameters);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
            await ReloadAsync();
    }

    private async Task CreateShareLink()
    {
        if (_tenantId is null || AuthenticationState is null)
            return;

        var parameters = new DialogParameters<CreateShareLinkDialog>();
        parameters.Add(x => x.TenantId, _tenantId);
        parameters.Add(x => x.CreatedByEmail,
            (await AuthenticationState).User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty);
        var dialog = await DialogService.ShowAsync<CreateShareLinkDialog>("New display link", parameters);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
            await ReloadAsync();
    }

    private async Task RevokeShareLink(ShareLink link)
    {
        if (_tenantId is null)
            return;

        _confirmationTitle = "Turn off this link?";
        _confirmationMessage =
            $"\"{link.Name}\" will stop working. A screen already showing it goes blank within five minutes. You can make a new link at any time.";
        StateHasChanged();
        if (true != await Confirmation.ShowAsync())
            return;

        var outcome = await RunAsync(
            async () => await ShareLinkService.RevokeAsync(link.Id, _tenantId, CancellationToken),
            successMessage: $"\"{link.Name}\" was turned off.");
        if (outcome.Succeeded)
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
