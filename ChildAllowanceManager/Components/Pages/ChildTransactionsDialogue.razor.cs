using ChildAllowanceManager.Common.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ChildAllowanceManager.Components.Pages;

public partial class ChildTransactionsDialogue : CancellableComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;
    [Parameter] public ChildWithBalance Child { get; set; } = default!;

    private bool _hideRegularTransactions = false;
}