using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChildAllowanceManager.Common.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    private string _email;
    
    [Column("email")]
    [Required]
    [MaxLength(254)]
    public string Email
    {
        get => _email?.ToLowerInvariant();
        set => _email = value.ToLowerInvariant();
    }
    
    [Column("name")]
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column("roles")]
    [Description("Roles for this user")]
    public string[] Roles { get; set; } = [];
    
    [Column("tenants")]
    [Description("The tenants that the user can access")]
    public string[] Tenants { get; set; } = Array.Empty<string>();
    
    [Column("last_logged_in")]
    public DateTimeOffset? LastLoggedIn { get; set; } = default!;
    
    [Column("deleted")]
    public bool Deleted { get; set; } = false;
    
    [Column("created_timestamp")]
    public DateTimeOffset CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_timestamp")]
    public DateTimeOffset UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public ICollection<UserTenant> UserTenants { get; set; } = new List<UserTenant>();
}