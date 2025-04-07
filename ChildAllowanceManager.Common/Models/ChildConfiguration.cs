using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChildAllowanceManager.Common.Models;

[Table("children")]
public class ChildConfiguration
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Column("first_name")]
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Column("last_name")]
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [Column("birth_date")]
    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }
    
    [Column("regular_allowance")]
    [DataType(DataType.Currency)]
    public decimal RegularAllowance { get; set; } = 1.0m;
    
    [Column("hold_days_remaining")]
    public int HoldDaysRemaining { get; set; } = 0;

    [Column("birthday_allowance")]
    [DataType(DataType.Currency)] 
    public decimal? BirthdayAllowance { get; set; } = null;
    
    [Column("tenant_id")]
    public string TenantId { get; set; } = Guid.NewGuid().ToString();
    
    [Column("deleted")]
    public bool Deleted { get; set; } = false;
    
    [Column("created_timestamp")]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    [Column("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(TenantId))]
    public virtual TenantConfiguration Tenant { get; set; }
    
    public virtual ICollection<AllowanceTransaction> AllowanceTransactions { get; set; } = new List<AllowanceTransaction>();
}