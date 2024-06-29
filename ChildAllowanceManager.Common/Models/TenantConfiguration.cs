using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Common.Models;

public class TenantConfiguration: BaseItem
{
    public string TenantName { get; set; }
    public string UrlSuffix { get; set; } = Guid.NewGuid().ToString("n")[..8];
}