using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Attributes;

namespace ChildAllowanceManager.Common.Models;

[PartitionKeyPath("/tenantId")]
public class AllowanceTransaction: BaseItem
{
    public decimal Balance { get; set; }
    public decimal TransactionAmount { get; set; }
    public string Description { get; set; }
    public string ChildId { get; set; }
    public string TenantId { get; set; }
    public DateTimeOffset TransactionTimestamp { get; set; }
    public TransactionType TransactionType { get; set; }

    protected override string GetPartitionKeyValue()
    {
        return TenantId;
    }
}