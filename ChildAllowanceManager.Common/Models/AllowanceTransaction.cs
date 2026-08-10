
namespace ChildAllowanceManager.Common.Models;

public class AllowanceTransaction: BaseItem
{
    public decimal Balance { get; set; }
    public decimal TransactionAmount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ChildId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTimeOffset TransactionTimestamp { get; set; }
    public TransactionType TransactionType { get; set; }

    public DateTime? AllowanceDate { get; set; }

    public ChildConfiguration? Child { get; set; }
    public TenantConfiguration? Tenant { get; set; }

}
