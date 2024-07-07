using Microsoft.AspNetCore.Components;

namespace ChildAllowanceManager.Components;

// from https://stackoverflow.com/questions/62499939/cancellationtoken-in-blazor-pages
public abstract class CancellableComponentBase : ComponentBase, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;

    protected CancellationToken CancellationToken => (_cancellationTokenSource ??= new()).Token;

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