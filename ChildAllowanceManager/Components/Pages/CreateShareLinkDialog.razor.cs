using ChildAllowanceManager.Common.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class CreateShareLinkDialog : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public string TenantId { get; set; } = default!;
    [Parameter] public string CreatedByEmail { get; set; } = default!;

    [Inject] private IShareLinkService ShareLinkService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    private MudForm _form = default!;
    private string _name = string.Empty;
    private int _expiryChoice;
    private bool _created;
    private CreatedShareLink? _result;

    private string ShareUrl => Navigation.ToAbsoluteUri($"/share/{_result!.Token}").ToString();

    private async Task CreateOrFinishAsync()
    {
        if (_created)
        {
            MudDialog.Close(DialogResult.Ok(true));
            return;
        }

        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        var expiresAt = _expiryChoice switch
        {
            30 => DateTimeOffset.UtcNow.AddDays(30),
            365 => DateTimeOffset.UtcNow.AddYears(1),
            _ => (DateTimeOffset?)null
        };
        CreatedShareLink? created = null;
        var outcome = await RunAsync(async () =>
            created = await ShareLinkService.CreateAsync(
                TenantId, _name, CreatedByEmail, expiresAt, CancellationToken));
        if (outcome.Succeeded)
        {
            _result = created;
            _created = true;
        }
    }

    private async Task CopyLinkAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", ShareUrl);
        }
        catch (JSException)
        {
        }
    }
}
