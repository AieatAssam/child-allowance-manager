namespace ChildAllowanceManager.Common.Models;

public class TenantConfiguration: BaseItem
{
    public string TenantName { get; set; } = string.Empty;
    public string UrlSuffix { get; set; } = Guid.NewGuid().ToString("n")[..8];

    /// IANA time zone id, for example "Europe/London". Allowance scheduling and all
    /// date display for this family happen in this zone. See decisions.D6_timezone_storage.
    public string TimeZoneId { get; set; } = "Europe/London";
}
