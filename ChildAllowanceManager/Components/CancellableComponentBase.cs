using Microsoft.AspNetCore.Components;

namespace ChildAllowanceManager.Components;

// from https://stackoverflow.com/questions/62499939/cancellationtoken-in-blazor-pages
public abstract class CancellableComponentBase : ComponentBase, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    [Inject] protected OperationRunner Operations { get; set; } = default!;

    /// True while a user-initiated operation is running. Bind submit buttons to
    /// Disabled="@IsBusy" so a double click cannot fire twice.
    protected bool IsBusy { get; private set; }

    protected CancellationToken CancellationToken => (_cancellationTokenSource ??= new()).Token;

    protected async Task<OperationOutcome> RunAsync(
        Func<Task> action,
        string? successMessage = null,
        string failureMessage = "Something went wrong. Nothing was changed.")
    {
        if (IsBusy) return new OperationOutcome(false, null);
        IsBusy = true;
        StateHasChanged();
        try
        {
            return await Operations.RunAsync(action, successMessage, failureMessage, CancellationToken);
        }
        finally
        {
            IsBusy = false;
            StateHasChanged();
        }
    }

    public virtual void Dispose()
    {
        if (_cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}
