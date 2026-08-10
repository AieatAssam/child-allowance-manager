using System.ComponentModel;
using System.Security.Claims;

namespace ChildAllowanceManager.Common.Models;

public class User : BaseItem
{
    private string _email = string.Empty;

    public string Email
    {
        get => _email;
        set => _email = value.Trim().ToLowerInvariant();
    }

    public string Name { get; set; } = string.Empty;

    [Description("Roles for this user")]
    public string[] Roles { get; set; } = [];
    
    [Description("The tenants that the user can access")]
    public string[] Tenants { get; set; } = Array.Empty<string>();

    public DateTimeOffset? LastLoggedIn { get; set; } = default!;

}
