using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChildAllowanceManager.Common.Models;

[Table("user_tenants")]
public class UserTenant
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Column("user_id")]
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Column("tenant_id")]
    [Required]
    public string TenantId { get; set; } = string.Empty;
    
    [Column("created_timestamp")]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    [Column("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public User User { get; set; } = null!;
    public TenantConfiguration Tenant { get; set; } = null!;
} 