using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Common.Models;

public class TenantConfiguration: Item
{
    public string TenantName { get; set; }
    public string UrlSuffix { get; set; } = Guid.NewGuid().ToString("n")[..8];
    public bool Deleted { get; set; }
    
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
}