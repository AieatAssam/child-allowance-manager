using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components;

public partial class ConfirmDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string Message { get; set; } = string.Empty;

    [Parameter]
    public string ConfirmText { get; set; } = "Delete";

    [Parameter]
    public bool Destructive { get; set; } = true;

    public static async Task<bool> ShowAsync(
        IDialogService dialogService,
        string title,
        string message,
        string confirmText = "Delete",
        bool destructive = true)
    {
        var parameters = new DialogParameters<ConfirmDialog>();
        parameters.Add(x => x.Title, title);
        parameters.Add(x => x.Message, message);
        parameters.Add(x => x.ConfirmText, confirmText);
        parameters.Add(x => x.Destructive, destructive);

        var dialog = await dialogService.ShowAsync<ConfirmDialog>(
            title,
            parameters,
            new DialogOptions { CloseOnEscapeKey = true, DefaultFocus = DefaultFocus.None });
        return (await dialog.Result)?.Canceled != true;
    }
}
