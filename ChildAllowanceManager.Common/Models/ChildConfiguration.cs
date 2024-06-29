using System.ComponentModel.DataAnnotations;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Attributes;

namespace ChildAllowanceManager.Common.Models;

[PartitionKeyPath("/tenantId")]
public class ChildConfiguration : BaseItem
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    
    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }
    
    [DataType(DataType.Currency)]
    public decimal RegularAllowance { get; set; } = 1.0m;

    [DataType(DataType.Currency)] public decimal? BirthdayAllowance { get; set; } = null;
    
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    
    protected override string GetPartitionKeyValue()
    {
        return TenantId;
    }
}