using Microsoft.Azure.CosmosRepository;

namespace ChildAllowanceManager.Common.Models;

public class TenantConfiguration: Item
{
    public string TenantName { get; set; }
    public string UrlSuffix { get; set; }
}