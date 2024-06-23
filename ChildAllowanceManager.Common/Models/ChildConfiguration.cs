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
    
    [DataType(DataType.Currency)]
    public decimal BirthdayAllowance { get; set; } = 10.0m;
    
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;
    public bool Deleted { get; set; } = false;
    
    public Guid TenantId { get; set; }

    protected override string GetPartitionKeyValue()
    {
        return TenantId.ToString();
    }
}