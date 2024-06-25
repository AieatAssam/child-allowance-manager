namespace ChildAllowanceManager.Common.Models;

public class ChildWithBalance
{
    public string Id { get; set; }
    public string TenantId { get; set; }
    public string Name { get; set; }
    public bool IsBirthday { get; set; }
    public decimal Balance { get; set; }
    public decimal NextRegularChange { get; set; }
    public DateTimeOffset NextRegularChangeDate { get; set; }
}