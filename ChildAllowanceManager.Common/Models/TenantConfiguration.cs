using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChildAllowanceManager.Common.Models;

[Table("tenants")]
public class TenantConfiguration
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    [Column("tenant_name")]
    [Required]
    [MaxLength(100)]
    public string TenantName { get; set; } = string.Empty;
    
    [Column("description")]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    [Column("url_suffix")]
    [Required]
    [MaxLength(255)]
    public string UrlSuffix { get; set; } = string.Empty;
    
    [Column("deleted")]
    public bool Deleted { get; set; } = false;
    
    [Column("created_timestamp")]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    [Column("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public ICollection<ChildConfiguration> Children { get; set; } = new List<ChildConfiguration>();
    public ICollection<AllowanceTransaction> AllowanceTransactions { get; set; } = new List<AllowanceTransaction>();
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
}