using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Azure.CosmosRepository;
using Microsoft.Azure.CosmosRepository.Attributes;

namespace ChildAllowanceManager.Common.Models;

[Table("children")]
public class ChildConfiguration
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Column("first_name")]
    public string FirstName { get; set; }
    
    [Column("last_name")]
    public string LastName { get; set; }
    
    [DataType(DataType.Date)]
    [Column("birth_date")]
    public DateTime? BirthDate { get; set; }
    
    [DataType(DataType.Currency)]
    [Column("regular_allowance")]
    public decimal RegularAllowance { get; set; } = 1.0m;
    
    [Column("hold_days_remaining")]
    public int HoldDaysRemaining { get; set; } = 0;

    [Column("birthday_allowance")]
    [DataType(DataType.Currency)] public decimal? BirthdayAllowance { get; set; } = null;
    
    [Column("tenant_id")]
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    
    [ForeignKey(nameof(TenantId))]
    public virtual TenantConfiguration Tenant { get; set; }
    
    public virtual List<AllowanceTransaction> AllowanceTransactions { get; set; } = new List<AllowanceTransaction>();
}