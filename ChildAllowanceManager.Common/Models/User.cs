using System.ComponentModel;
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

    [Description("Additional claims for this user")]
    public string[] ExtraClaims { get; set; } = Array.Empty<string>();
    
    [Description("The tenants that the user can access")]
    public string[] Tenants { get; set; } = Array.Empty<string>();

    protected override string GetPartitionKeyValue()
    {
        return _email;
    }
}