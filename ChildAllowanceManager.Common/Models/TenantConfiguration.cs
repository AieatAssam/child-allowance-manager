namespace ChildAllowanceManager.Common.Models;

public class TenantConfiguration: BaseItem
{
    public string TenantName { get; set; } = string.Empty;
    public string UrlSuffix { get; set; } = Guid.NewGuid().ToString("n")[..8];
}
