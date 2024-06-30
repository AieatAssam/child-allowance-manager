using System.ComponentModel;
using System.Security.Claims;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Attributes;

namespace ChildAllowanceManager.Common.Models;

[PartitionKeyPath("/email")]
public class User : BaseItem
{
    private string _email;

    public string Email
    {
        get => _email.ToLowerInvariant();
        set => _email = value.ToLowerInvariant();
    }

    public string Name { get; set; }

    [Description("Roles for this user")]
    public string[] Roles { get; set; } = [];
    
    [Description("The tenants that the user can access")]
    public string[] Tenants { get; set; } = Array.Empty<string>();

    public DateTimeOffset? LastLoggedIn { get; set; } = default!;

    protected override string GetPartitionKeyValue()
    {
        return _email;
    }
}