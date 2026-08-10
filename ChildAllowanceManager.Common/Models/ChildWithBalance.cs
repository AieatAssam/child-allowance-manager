namespace ChildAllowanceManager.Common.Models;

public class ChildWithBalance
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBirthday { get; set; }
    public decimal Balance { get; set; }
    
    public int HoldDaysRemaining { get; set; }
    public decimal NextRegularChange { get; set; }
    public DateTimeOffset NextRegularChangeDate { get; set; }

    /// IANA zone id of the family, so the UI can render an exact local date without
    /// guessing the server's zone.
    public string TimeZoneId { get; set; } = "Europe/London";

    /// The family's local calendar date of the next allowance. Render this, not a
    /// server-local conversion.
    public DateOnly NextRegularChangeLocalDate { get; set; }
}
