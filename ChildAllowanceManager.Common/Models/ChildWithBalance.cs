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
}
