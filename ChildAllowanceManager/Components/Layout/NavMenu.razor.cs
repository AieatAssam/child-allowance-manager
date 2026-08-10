
using Microsoft.AspNetCore.Components;


namespace ChildAllowanceManager.Components.Layout;

public partial class NavMenu
{
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    public string? TenantSuffix { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            var segments = Navigation.ToBaseRelativePath(Navigation.Uri)
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2 && segments[1] is "children" or "configuration")
            {
                TenantSuffix = segments[0];
                StateHasChanged();
            }
        }
    }
}
    
