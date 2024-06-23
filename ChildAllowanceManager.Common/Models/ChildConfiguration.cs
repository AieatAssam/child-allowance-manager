using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Attributes;

namespace ChildAllowanceManager.Common.Models;

[PartitionKeyPath("/tenantId")]
public class ChildConfiguration : Item
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }
    
    [DataType(DataType.Currency)]
    public decimal RegularAllowance { get; set; } = 1.0m;

    [DataType(DataType.Currency)] public decimal? BirthdayAllowance { get; set; } = null;
    
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool Deleted { get; set; } = false;

    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    protected override string GetPartitionKeyValue()
    {
        return TenantId;
    }
}