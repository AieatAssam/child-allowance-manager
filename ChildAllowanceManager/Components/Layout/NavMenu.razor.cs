
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;


namespace ChildAllowanceManager.Components.Layout;

public partial class NavMenu : IDisposable
{
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    public string? TenantSuffix { get; set; }

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
        UpdateTenantSuffix(Navigation.Uri);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        var previous = TenantSuffix;
        UpdateTenantSuffix(args.Location);
        if (previous != TenantSuffix)
        {
            _ = InvokeAsync(StateHasChanged);
        }
    }

    private void UpdateTenantSuffix(string uri)
    {
        var segments = Navigation.ToBaseRelativePath(uri)
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        TenantSuffix = segments.Length == 2 && segments[1] is "children" or "configuration" or "people"
            ? segments[0]
            : null;
    }

    public override void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        base.Dispose();
    }
}
    
